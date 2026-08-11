import numpy as np
import cv2
import joblib
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, accuracy_score, confusion_matrix

# ==========================================
# Image Forgery Detection Model Trainer
# ==========================================
# This script trains a classifier to detect image manipulation using
# Error Level Analysis (ELA). ELA works by re-compressing an image at
# a known quality level and measuring pixel differences. Authentic images
# that have been uniformly compressed show consistent, low ELA residuals.
# Manipulated images contain regions edited after the last compression,
# producing higher variance in the difference map.
#
# Features extracted (must match app.py):
#   1. mean(diff)  — average pixel deviation after re-compression
#   2. var(diff)   — variance of pixel deviations (key forgery indicator)
#   3. max(diff)   — peak pixel deviation
# ==========================================

np.random.seed(42)

def apply_ela(image, quality=90):
    """
    Perform Error Level Analysis on an image.
    Compresses the image to JPEG at the given quality, then computes the
    absolute pixel difference between the original and re-compressed version.
    Returns the 3-feature vector: [mean, variance, max].
    """
    encode_ok, compressed_buf = cv2.imencode('.jpg', image, [cv2.IMWRITE_JPEG_QUALITY, quality])
    if not encode_ok:
        return [0.0, 0.0, 0.0]
    compressed = cv2.imdecode(compressed_buf, cv2.IMREAD_COLOR)
    diff = cv2.absdiff(image, compressed)
    return [float(np.mean(diff)), float(np.var(diff)), float(np.max(diff))]


def generate_authentic_image(width=256, height=256):
    """
    Generate a synthetic 'authentic' image by creating a natural-looking scene
    and compressing it through JPEG once (simulating a camera output).
    """
    img = np.zeros((height, width, 3), dtype=np.uint8)

    # Random smooth gradient background
    base_color = np.random.randint(40, 200, size=3)
    for y in range(height):
        ratio = y / height
        color = (base_color * (1 - ratio * 0.5)).astype(np.uint8)
        img[y, :] = color

    # Add random geometric shapes to simulate scene content
    num_shapes = np.random.randint(3, 8)
    for _ in range(num_shapes):
        shape_type = np.random.choice(['circle', 'rectangle', 'line'])
        color = tuple(np.random.randint(0, 255, size=3).tolist())

        if shape_type == 'circle':
            center = (np.random.randint(30, width - 30), np.random.randint(30, height - 30))
            radius = np.random.randint(10, 50)
            cv2.circle(img, center, radius, color, -1)
        elif shape_type == 'rectangle':
            pt1 = (np.random.randint(0, width - 40), np.random.randint(0, height - 40))
            pt2 = (pt1[0] + np.random.randint(20, 80), pt1[1] + np.random.randint(20, 80))
            cv2.rectangle(img, pt1, pt2, color, -1)
        else:
            pt1 = (np.random.randint(0, width), np.random.randint(0, height))
            pt2 = (np.random.randint(0, width), np.random.randint(0, height))
            cv2.line(img, pt1, pt2, color, np.random.randint(1, 4))

    # Add mild Gaussian noise (camera sensor noise)
    noise = np.random.normal(0, 5, img.shape).astype(np.int16)
    img = np.clip(img.astype(np.int16) + noise, 0, 255).astype(np.uint8)

    # Simulate camera JPEG output — compress once at high quality
    _, buf = cv2.imencode('.jpg', img, [cv2.IMWRITE_JPEG_QUALITY, 95])
    img = cv2.imdecode(buf, cv2.IMREAD_COLOR)

    return img


def generate_forged_image(width=256, height=256):
    """
    Generate a synthetic 'forged' image by taking an authentic image and
    splicing in a foreign region or applying localized edits that break
    the uniform compression signature.
    """
    # Start with an authentic base
    img = generate_authentic_image(width, height)

    # Apply one or more manipulation techniques
    manipulation = np.random.choice(['splice', 'brightness', 'clone', 'composite'])

    if manipulation == 'splice':
        # Paste a completely different uncompressed block (foreign region)
        block_w = np.random.randint(40, width // 2)
        block_h = np.random.randint(40, height // 2)
        x = np.random.randint(0, width - block_w)
        y = np.random.randint(0, height - block_h)
        # Uncompressed foreign content — distinct from the JPEG-compressed base
        foreign = np.random.randint(0, 255, (block_h, block_w, 3), dtype=np.uint8)
        img[y:y + block_h, x:x + block_w] = foreign

    elif manipulation == 'brightness':
        # Artificially brighten a region (like editing exposure in Photoshop)
        block_w = np.random.randint(60, width // 2)
        block_h = np.random.randint(60, height // 2)
        x = np.random.randint(0, width - block_w)
        y = np.random.randint(0, height - block_h)
        region = img[y:y + block_h, x:x + block_w].astype(np.int16)
        boost = np.random.randint(40, 100)
        region = np.clip(region + boost, 0, 255).astype(np.uint8)
        img[y:y + block_h, x:x + block_w] = region

    elif manipulation == 'clone':
        # Copy-move: duplicate one region of the image to another location
        block_size = np.random.randint(30, 60)
        src_x = np.random.randint(0, width - block_size)
        src_y = np.random.randint(0, height - block_size)
        dst_x = np.random.randint(0, width - block_size)
        dst_y = np.random.randint(0, height - block_size)
        cloned = img[src_y:src_y + block_size, src_x:src_x + block_size].copy()
        # Apply slight transform to cloned region (scale/rotation artifacts)
        M = cv2.getRotationMatrix2D((block_size // 2, block_size // 2), np.random.randint(-15, 15), 1.05)
        cloned = cv2.warpAffine(cloned, M, (block_size, block_size))
        img[dst_y:dst_y + block_size, dst_x:dst_x + block_size] = cloned

    elif manipulation == 'composite':
        # Blend two uncompressed images together (compositing)
        overlay = np.random.randint(50, 200, (height, width, 3), dtype=np.uint8)
        alpha = np.random.uniform(0.3, 0.6)
        mask = np.zeros((height, width), dtype=np.float32)
        cx, cy = width // 2, height // 2
        cv2.circle(mask, (cx, cy), np.random.randint(40, 80), 1.0, -1)
        mask = cv2.GaussianBlur(mask, (21, 21), 10)
        mask_3ch = np.stack([mask] * 3, axis=-1)
        img = (img * (1 - mask_3ch * alpha) + overlay * mask_3ch * alpha).astype(np.uint8)

    return img


# ==========================================
# 1. Generate Synthetic Dataset
# ==========================================
N_AUTHENTIC = 2000
N_FORGED = 2000

print(f"1. Generating {N_AUTHENTIC + N_FORGED} synthetic images for ELA training...")
print(f"   - {N_AUTHENTIC} authentic (uniformly JPEG-compressed)")
print(f"   - {N_FORGED} forged (post-compression edits)")

features_list = []
labels_list = []

for i in range(N_AUTHENTIC):
    img = generate_authentic_image()
    ela_features = apply_ela(img, quality=90)
    features_list.append(ela_features)
    labels_list.append(0)  # 0 = Authentic
    if (i + 1) % 500 == 0:
        print(f"   Authentic: {i + 1}/{N_AUTHENTIC}")

for i in range(N_FORGED):
    img = generate_forged_image()
    ela_features = apply_ela(img, quality=90)
    features_list.append(ela_features)
    labels_list.append(1)  # 1 = Forged
    if (i + 1) % 500 == 0:
        print(f"   Forged: {i + 1}/{N_FORGED}")

X = np.array(features_list)
y = np.array(labels_list)

print(f"\n   Feature matrix shape: {X.shape}")
print(f"   Feature names: [mean_diff, var_diff, max_diff]")
print(f"   Authentic mean ELA: mean={X[y==0, 0].mean():.3f}, var={X[y==0, 1].mean():.3f}, max={X[y==0, 2].mean():.1f}")
print(f"   Forged mean ELA:    mean={X[y==1, 0].mean():.3f}, var={X[y==1, 1].mean():.3f}, max={X[y==1, 2].mean():.1f}")

# ==========================================
# 2. Train/Test Split
# ==========================================
print("\n2. Splitting data (80% train, 20% test, stratified)...")
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

# ==========================================
# 3. Train Random Forest Classifier
# ==========================================
print("\n3. Training Random Forest classifier on ELA features...")
clf = RandomForestClassifier(
    n_estimators=200,
    max_depth=10,
    min_samples_split=5,
    random_state=42,
    n_jobs=-1
)
clf.fit(X_train, y_train)

# ==========================================
# 4. Evaluate
# ==========================================
print("\n4. Evaluating the Image Forgery Model...")
y_pred = clf.predict(X_test)

print(f"   Accuracy: {accuracy_score(y_test, y_pred) * 100:.2f}%\n")
print("Classification Report:")
print(classification_report(y_test, y_pred, target_names=["Authentic", "Forged"]))

print("Confusion Matrix:")
cm = confusion_matrix(y_test, y_pred)
print(f"   True Negatives (Authentic correctly identified):  {cm[0][0]}")
print(f"   False Positives (Authentic wrongly flagged):      {cm[0][1]}")
print(f"   False Negatives (Forged missed):                  {cm[1][0]}")
print(f"   True Positives (Forged correctly detected):       {cm[1][1]}")

# Feature importance
feature_names = ["mean_diff", "var_diff", "max_diff"]
importances = clf.feature_importances_
print("\nFeature Importances:")
for name, imp in sorted(zip(feature_names, importances), key=lambda x: -x[1]):
    print(f"   {name}: {imp:.4f}")

# ==========================================
# 5. Save Model
# ==========================================
print("\n5. Saving model for production...")
model_filename = "image_forgery_model.pkl"
joblib.dump(clf, model_filename)
print(f"   Success! Model saved as '{model_filename}'.")
print(f"   Your Flask app (app.py) will load this automatically at startup.")
