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
try:
    xgb_model = xgb.XGBClassifier()
    xgb_model.load_model("fraud_model.json")
    explainer = shap.TreeExplainer(xgb_model)
except Exception as e:
    print("Warning: Fraud model not loaded.")
    xgb_model = None

try:
    image_clf = joblib.load("image_forgery_model.pkl")
except Exception as e:
    print("Warning: image_forgery_model.pkl not found.")
    image_clf = None

# Initialize the Pre-Trained Inappropriate Content Detector
try:
    print("Loading NSFW Image Classifier... (This may take a moment the first time)")
    nsfw_detector = pipeline("image-classification", model="Falconsai/nsfw_image_detection")
except Exception as e:
    print("Warning: NSFW detector failed to load.")
    nsfw_detector = None

KNOWN_FRAUD_HASHES = [
    "5d41402abc4b2a76b9719d911017c592",
    "7d793037a0760186574b0282f2f435e7"
]

# ==========================================
# 2. Fraud Detection & XAI Endpoint
# ==========================================
@app.route('/api/ai/evaluate-risk', methods=['POST'])
def evaluate_risk():
    data = request.get_json()
    
    features = np.array([[
        data.get("transactionAmount", 0),
        data.get("accountAgeDays", 0),
        data.get("failedLoginAttempts", 0),
        data.get("distanceFromShippingAddress", 0)
    ]])

    risk_score = 0.0
    shap_data_json = "{}"
    
    if xgb_model:
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
# 3. Image Forgery & Inappropriate Content Endpoint
# ==========================================
@app.route('/api/ai/scan-image', methods=['POST'])
def scan_image():
    image_bytes = request.data
    
    # Check 1: Cryptographic Sieve (MD5 Hashing)
    image_hash = hashlib.md5(image_bytes).hexdigest()
    if image_hash in KNOWN_FRAUD_HASHES:
        return jsonify({
            "isForgeryDetected": True,
            "forgeryReason": f"MD5 Hash Match: Image ({image_hash}) is in the known fraudulent database."
        })

    # Check 2: Inappropriate / NSFW Content Detection
    if nsfw_detector:
        try:
            # Convert raw bytes into a PIL Image for the Hugging Face model
            img_pil = Image.open(io.BytesIO(image_bytes))
            
            # The model returns a list of dictionaries, e.g., [{'label': 'nsfw', 'score': 0.98}, ...]
            nsfw_result = nsfw_detector(img_pil)
            top_prediction = nsfw_result[0]
            
            # If the AI is over 80% confident the image is explicit, block it immediately
            if top_prediction['label'] == 'nsfw' and top_prediction['score'] > 0.80:
                return jsonify({
                    "isForgeryDetected": True,
                    "forgeryReason": f"Inappropriate content detected (Confidence: {top_prediction['score'] * 100:.1f}%)."
                })
        except Exception as e:
            print(f"NSFW Scan Error: {e}")

    # Check 3: Pixel-Level Analysis using OpenCV (ELA)
    np_arr = np.frombuffer(image_bytes, np.uint8)
    original = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)
    
    if original is None:
        return jsonify({"isForgeryDetected": True, "forgeryReason": "Corrupted image file."})

    temp_path = "temp_scan.jpg"
    cv2.imwrite(temp_path, original, [cv2.IMWRITE_JPEG_QUALITY, 90])
    compressed = cv2.imread(temp_path)
    
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

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)