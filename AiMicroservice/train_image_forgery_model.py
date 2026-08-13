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
# Error Level Analysis (ELA).
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

import os

def load_imd2020_dataset(dataset_dir):
    """
    Loads real and forged images from the IMD2020 dataset directory structure.
    IMD2020 typically contains subfolders for each image pair, containing
    the original image, the manipulated image, and a mask.
    """
    features_list = []
    labels_list = []
    
    print(f"Scanning directory: {dataset_dir}")
    
    if not os.path.exists(dataset_dir):
        raise FileNotFoundError(f"Dataset directory not found: {dataset_dir}")

    total_authentic = 0
    total_forged = 0

    for root, dirs, files in os.walk(dataset_dir):
        for file in files:
            file_lower = file.lower()
            if file_lower.endswith(('.png', '.jpg', '.jpeg', '.tif', '.tiff')):
                filepath = os.path.join(root, file)
                
                # In IMD2020, masks usually end with '_mask.png' or contain 'mask'
                if '_mask' in file_lower or 'mask' in file_lower:
                    continue
                    
                # Assign label based on filename (orig = 0, manipulated = 1)
                if '_orig' in file_lower or 'orig' in file_lower or 'real' in file_lower or 'authentic' in file_lower:
                    label = 0
                else:
                    label = 1
                
                try:
                    img = cv2.imread(filepath)
                    if img is None:
                        continue
                        
                    ela_features = apply_ela(img, quality=90)
                    
                    features_list.append(ela_features)
                    labels_list.append(label)

                    if label == 0:
                        total_authentic += 1
                    else:
                        total_forged += 1

                except Exception as e:
                    print(f"Error processing {filepath}: {e}")
                    
    print(f"Finished scanning. Found {total_authentic} authentic and {total_forged} forged images.")
    return np.array(features_list), np.array(labels_list)


# ==========================================
# 1. Load Real-World Dataset (IMD2020)
# ==========================================
dataset_path = "C:/Users/zeyang/Downloads/IMD2020"
print(f"1. Loading real-world IMD2020 dataset from: {dataset_path}")
X, y = load_imd2020_dataset(dataset_path)

if len(X) == 0:
    print("Error: No valid images found! Please make sure you have extracted the IMD2020.zip file to C:\\Users\\zeyang\\Downloads\\IMD2020")
    exit(1)

print(f"\n   Feature matrix shape: {X.shape}")
print(f"   Feature names: [mean_diff, var_diff, max_diff]")
if len(X[y==0]) > 0:
    print(f"   Authentic mean ELA: mean={X[y==0, 0].mean():.3f}, var={X[y==0, 1].mean():.3f}, max={X[y==0, 2].mean():.1f}")
if len(X[y==1]) > 0:
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
