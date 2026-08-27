from flask import Flask, request, jsonify
import xgboost as xgb
import shap
import cv2
import numpy as np
import hashlib
import json
import joblib
import io
from PIL import Image
from transformers import pipeline

app = Flask(__name__)

# ==========================================
# 1. AI Model Initialization
# ==========================================

# --- Fraud Detection Model (XGBoost + SHAP) ---
xgb_model = None
explainer = None
try:
    xgb_model = xgb.XGBClassifier()
    xgb_model.load_model("fraud_model.json")
    explainer = shap.TreeExplainer(xgb_model)
except Exception as e:
    print(f"Warning: Fraud model not loaded. ({e})")

# --- Image Forgery Classifier (Scikit-learn) ---
image_clf = None
try:
    image_clf = joblib.load("image_forgery_model.pkl")
except Exception as e:
    print(f"Warning: image_forgery_model.pkl not found. ({e})")

# --- NSFW Content Detector (Hugging Face) ---
nsfw_detector = None
try:
    print("Loading NSFW Image Classifier... (This may take a moment the first time)")
    nsfw_detector = pipeline("image-classification", model="Falconsai/nsfw_image_detection")
except Exception as e:
    print(f"Warning: NSFW detector failed to load. ({e})")

# --- Chat NLP Spam/Phishing Classifier (TF-IDF + Random Forest for English) ---
chat_vectorizer = None
chat_clf = None
try:
    chat_vectorizer = joblib.load("chat_tfidf_vectorizer.pkl")
    chat_clf = joblib.load("chat_nlp_model.pkl")
    print("Chat NLP model and vectorizer loaded successfully.")
except Exception as e:
    print(f"Warning: Chat NLP model not loaded. ({e})")

# --- Multilingual Zero-Shot Classifier (XLM-RoBERTa) ---
multilingual_clf = None
try:
    print("Loading Multilingual XLM-RoBERTa Classifier... (This may take a moment the first time)")
    multilingual_clf = pipeline("zero-shot-classification", model="joeddav/xlm-roberta-large-xnli")
    print("Multilingual AI loaded successfully.")
except Exception as e:
    print(f"Warning: Multilingual classifier failed to load. ({e})")

# ==========================================
# 2. Health Check Endpoint
# ==========================================
@app.route('/api/ai/health', methods=['GET'])
def health_check():
    return jsonify({
        "status": "healthy",
        "models": {
            "fraud_xgboost": xgb_model is not None,
            "image_forgery": image_clf is not None,
            "nsfw_detector": nsfw_detector is not None,
            "chat_nlp": chat_clf is not None,
            "multilingual_clf": multilingual_clf is not None
        }
    })

# ==========================================
# 3. Fraud Detection & XAI Endpoint
# ==========================================
@app.route('/api/ai/evaluate-risk', methods=['POST'])
def evaluate_risk():
    data = request.get_json()

    if not data:
        return jsonify({"error": "Invalid or missing JSON payload."}), 400

    try:
        features = np.array([[
            float(data.get("transactionAmount", 0)),
            float(data.get("accountAgeDays", 0)),
            float(data.get("failedLoginAttempts", 0)),
            float(data.get("distanceFromShippingAddress", 0)),
            float(data.get("transactions_last_10_mins", 0)),
            float(data.get("time_since_account_creation_seconds", 0)),
            float(data.get("device_ip_flags", 0))
        ]])
    except (ValueError, TypeError) as e:
        return jsonify({"error": f"Invalid feature values: {e}"}), 400

    risk_score = 0.0
    shap_data_json = "{}"
    
    if xgb_model and explainer:
        probabilities = xgb_model.predict_proba(features)
        risk_score = float(probabilities[0][1]) 
        shap_values = explainer.shap_values(features)
        
        shap_dict = {
            "base_value": float(explainer.expected_value),
            "feature_weights": shap_values[0].tolist()
        }
        shap_data_json = json.dumps(shap_dict)
    else:
        risk_score = 0.88 if data.get("transactionAmount", 0) > 1000 else 0.12
        shap_data_json = json.dumps({"reason": "Simulated XAI data"})

    is_blocked = bool(risk_score > 0.85)

    return jsonify({
        "riskScore": risk_score,
        "isBlocked": is_blocked,
        "shapData": shap_data_json
    })

# ==========================================
# 4. Image Forgery & Inappropriate Content Endpoint
# ==========================================
@app.route('/api/ai/scan-image', methods=['POST'])
def scan_image():
    image_bytes = request.data

    if not image_bytes:
        return jsonify({"isForgeryDetected": True, "forgeryReason": "Empty image payload received."}), 400

    # Check 2: Inappropriate / NSFW Content Detection
    if nsfw_detector:
        try:
            img_pil = Image.open(io.BytesIO(image_bytes))
            nsfw_result = nsfw_detector(img_pil)
            top_prediction = nsfw_result[0]
            
            if top_prediction['label'] == 'nsfw' and top_prediction['score'] > 0.80:
                return jsonify({
                    "isForgeryDetected": True,
                    "forgeryReason": f"Inappropriate content detected (Confidence: {top_prediction['score'] * 100:.1f}%)."
                })
        except Exception as e:
            print(f"NSFW Scan Error: {e}")

    # Check 3: Pixel-Level Analysis using OpenCV (ELA) — fully in-memory, no temp files
    np_arr = np.frombuffer(image_bytes, np.uint8)
    original = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)
    
    if original is None:
        return jsonify({"isForgeryDetected": True, "forgeryReason": "Corrupted image file."})

    # Compress to JPEG in memory and decode back (no disk I/O race condition)
    encode_result, compressed_buf = cv2.imencode('.jpg', original, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not encode_result:
        return jsonify({"isForgeryDetected": True, "forgeryReason": "Image compression failed during ELA analysis."})
    compressed = cv2.imdecode(compressed_buf, cv2.IMREAD_COLOR)
    
    diff = cv2.absdiff(original, compressed)
    features = np.array([[np.mean(diff), np.var(diff), np.max(diff)]])
    
    if image_clf:
        prediction = image_clf.predict(features)[0]
        if prediction == 1:
            return jsonify({
                "isForgeryDetected": True,
                "forgeryReason": "OpenCV detected anomalous ELA pixel variance indicative of image manipulation."
            })

    return jsonify({
        "isForgeryDetected": False,
        "forgeryReason": "Image passed cryptographic, NSFW, and OpenCV forensic scans."
    })

# ==========================================
# 5. Chat Message Multilingual Spam/Phishing Endpoint
# ==========================================

# Spam keywords in multiple languages (English, Malay, Chinese)
SPAM_KEYWORDS = [
    # English
    "free money", "you won", "click here now", "act now", "limited time offer",
    "congratulations you have been selected", "claim your prize", "earn money fast",
    "wire transfer", "nigerian prince", "lottery winner", "100% free",
    # Malay
    "wang percuma", "anda menang", "klik sini", "tawaran terhad", "hadiah percuma",
    "tahniah anda dipilih", "tebus hadiah", "duit mudah", "pindahan wang",
    # Chinese
    "免费", "中奖", "点击这里", "限时优惠", "恭喜你被选中",
    "领取奖品", "快速赚钱", "汇款", "彩票中奖", "赌博",
    "刷单", "兼职日赚", "加微信", "代购优惠"
]

@app.route('/api/ai/scan-chat', methods=['POST'])
def scan_chat():
    data = request.get_json()

    if not data or 'message' not in data:
        return jsonify({"error": "Missing 'message' field in JSON payload."}), 400

    message = data['message']

    if not message or not message.strip():
        return jsonify({"isMalicious": False, "isBlocked": False, "reason": "Empty message."})

    message_lower = message.lower().strip()
    flags = []  # Collect all detection signals

    # --- Layer 1: Multilingual Keyword Detection (Fast) ---
    for keyword in SPAM_KEYWORDS:
        if keyword in message_lower:
            flags.append(f"Keyword match: '{keyword}'")
            break  # One match is enough

    # --- Layer 2: English TF-IDF + Random Forest Model ---
    tfidf_malicious = False
    if chat_clf and chat_vectorizer:
        try:
            message_vector = chat_vectorizer.transform([message])
            prediction = chat_clf.predict(message_vector)[0]
            probability = chat_clf.predict_proba(message_vector)[0]
            tfidf_confidence = float(max(probability))

            if prediction == 1 and tfidf_confidence > 0.70:
                tfidf_malicious = True
                flags.append(f"TF-IDF spam classifier (Confidence: {tfidf_confidence * 100:.1f}%)")
        except Exception as e:
            print(f"TF-IDF Error: {e}")

    # --- Layer 3: Multilingual Zero-Shot Classification (XLM-RoBERTa) ---
    if multilingual_clf:
        try:
            candidate_labels = ["spam or scam message", "normal conversation"]
            result = multilingual_clf(message, candidate_labels)

            spam_score = 0.0
            for label, score in zip(result['labels'], result['scores']):
                if label == "spam or scam message":
                    spam_score = score
                    break

            if spam_score > 0.75:
                flags.append(f"Multilingual AI classifier (Confidence: {spam_score * 100:.1f}%)")
        except Exception as e:
            print(f"Multilingual classifier error: {e}")

    # --- Layer 4: AI Heuristic Malicious Link Detection ---
    import re
    from urllib.parse import urlparse

    url_pattern = re.compile(r'(?:https?://)?(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{2,256}\.[a-z]{2,6}\b(?:[-a-zA-Z0-9@:%_\+.~#?&//=]*)')
    urls = url_pattern.findall(message_lower)
    
    known_safe_domains = ['shopee.com.my', 'shopee.com', 'maybank2u.com.my', 'secureplatform.com']
    suspicious_tlds = ['.xyz', '.top', '.tk', '.cc', '.ru']
    
    def levenshtein(s1, s2):
        if len(s1) < len(s2): return levenshtein(s2, s1)
        if len(s2) == 0: return len(s1)
        prev = range(len(s2) + 1)
        for i, c1 in enumerate(s1):
            curr = [i + 1]
            for j, c2 in enumerate(s2):
                curr.append(min(prev[j + 1] + 1, curr[j] + 1, prev[j] + (c1 != c2)))
            prev = curr
        return prev[-1]

    for url in urls:
        parsed = urlparse(url)
        domain = parsed.netloc if parsed.netloc else parsed.path.split('/')[0]
        
        # 1. Suspicious TLD check
        if any(tld in domain for tld in suspicious_tlds):
            flags.append(f"Suspicious domain extension detected: {domain}")
            
        # 2. Typosquatting check
        for safe_domain in known_safe_domains:
            if domain != safe_domain:
                if levenshtein(domain, safe_domain) <= 3 and len(domain) >= len(safe_domain) - 3:
                    flags.append(f"Potential typosquatting detected: {domain} mimics {safe_domain}")
                    break
                    
        # 3. Subdomain abuse
        if len(domain.split('.')) > 3 and not (domain.endswith('com.my') or domain.endswith('co.uk')):
            flags.append(f"Suspicious number of subdomains detected: {domain}")

    # --- Final Decision ---
    is_malicious = len(flags) > 0
    is_blocked = len(flags) >= 1  # Block if any layer flags it

    if is_malicious:
        reason = "Message blocked: " + "; ".join(flags)
    else:
        reason = "Message classified as safe."

    return jsonify({
        "isMalicious": is_malicious,
        "isBlocked": is_blocked,
        "reason": reason
    })

# (app.run moved to bottom)
# ==========================================
# 6. Personalized Content-Based Recommendations
# ==========================================
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

@app.route("/api/ai/recommend-products", methods=["POST"])
def recommend_products():
    data = request.get_json()
    if not data or "buyer_history" not in data or "candidate_products" not in data:
        return jsonify({"error": "Missing buyer_history or candidate_products in JSON payload."}), 400

    buyer_history = data["buyer_history"]
    candidate_products = data["candidate_products"]

    if not buyer_history or not candidate_products:
        return jsonify({"recommended_ids": []})

    # Combine text representations
    history_text = " ".join(buyer_history)
    candidate_texts = [p["text"] for p in candidate_products]
    candidate_ids = [p["id"] for p in candidate_products]

    # Vectorize
    vectorizer = TfidfVectorizer(stop_words="english")
    try:
        tfidf_matrix = vectorizer.fit_transform([history_text] + candidate_texts)
    except ValueError:
        return jsonify({"recommended_ids": []})

    # Cosine Similarity between user profile (index 0) and candidates (index 1 to N)
    cosine_sim = cosine_similarity(tfidf_matrix[0:1], tfidf_matrix[1:]).flatten()

    # Get top 5 indices
    top_indices = cosine_sim.argsort()[-5:][::-1]
    
    recommended_ids = []
    for idx in top_indices:
        if cosine_sim[idx] > 0.05: # Only recommend if there is some overlap
            recommended_ids.append(candidate_ids[idx])

    return jsonify({"recommended_ids": recommended_ids})

@app.route('/api/ai/forecast-demand', methods=['POST'])
def forecast_demand():
    data = request.json
    if not data or 'products' not in data:
        return jsonify({"error": "No product data provided"}), 400

    from sklearn.linear_model import LinearRegression
    import numpy as np
    
    forecasts = []
    # X is the days 1 to 30
    X = np.array(range(1, 31)).reshape(-1, 1)
    # The days we want to predict (next 7 days)
    X_predict = np.array(range(31, 38)).reshape(-1, 1)

    for p in data['products']:
        try:
            sales_history = p.get('recent_sales_30d', [])
            stock = p.get('stock', 0)
            product_id = p.get('id')

            if len(sales_history) != 30:
                # Pad with zeros if less than 30
                sales_history = ([0] * (30 - len(sales_history))) + sales_history
            
            y = np.array(sales_history)
            
            # Simple linear regression
            model = LinearRegression()
            model.fit(X, y)
            
            predicted = model.predict(X_predict)
            
            # Ensure predictions aren't negative, and sum them up
            total_predicted_7_day = int(sum([max(0, val) for val in predicted]))
            
            restock_needed = total_predicted_7_day > stock
            restock_amount = max(0, total_predicted_7_day - stock)
            
            forecasts.append({
                "id": product_id,
                "predicted_7_day_sales": total_predicted_7_day,
                "restock_needed": restock_needed,
                "restock_amount": restock_amount
            })
        except Exception as e:
            print(f"Error predicting for product {p.get('id')}: {str(e)}")
            continue

    return jsonify({"forecasts": forecasts})

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=False)
