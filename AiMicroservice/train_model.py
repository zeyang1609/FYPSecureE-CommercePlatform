import pandas as pd
import numpy as np
import xgboost as xgb
from imblearn.over_sampling import SMOTE
from sklearn.model_selection import train_test_split
from sklearn.metrics import average_precision_score, classification_report

print("1. Generating highly imbalanced synthetic e-commerce data...")
# Simulating 10,000 transactions where only ~2% are fraudulent
np.random.seed(42)
n_samples = 10000

# Features: transactionAmount, accountAgeDays, failedLoginAttempts, distanceFromShippingAddress
X_legit = np.random.normal(loc=[120, 365, 0, 15], scale=[50, 100, 0, 10], size=(9800, 4))
y_legit = np.zeros(9800) # 0 = Legitimate

# Fraudsters typically have high transaction amounts, new accounts, failed logins, and high geographic distance
X_fraud = np.random.normal(loc=[800, 5, 4, 500], scale=[200, 10, 2, 100], size=(200, 4))
y_fraud = np.ones(200) # 1 = Fraud

# Combine into a single dataset
X = np.vstack((X_legit, X_fraud))
y = np.hstack((y_legit, y_fraud))

# Ensure no negative values for realistic data
X = np.clip(X, 0, None)

# Split into training and testing sets (80% train, 20% test)
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42, stratify=y)

print(f"Original Training Data Shape: {X_train.shape}. Fraud cases: {sum(y_train)}")

print("\n2. Applying SMOTE to balance the training data...")
# SMOTE generates synthetic data points along the line segments joining k-nearest neighbors
smote = SMOTE(random_state=42)
X_train_smote, y_train_smote = smote.fit_resample(X_train, y_train)

print(f"SMOTE Training Data Shape: {X_train_smote.shape}. Fraud cases: {sum(y_train_smote)}")

print("\n3. Training the XGBoost Model...")
# XGBoost builds trees sequentially to optimize residual errors
# Define feature names for SHAP interpretability
FEATURE_NAMES = ["transactionAmount", "accountAgeDays", "failedLoginAttempts", "distanceFromShippingAddress"]

xgb_model = xgb.XGBClassifier(
    objective='binary:logistic',
    eval_metric='aucpr', # Prioritize Area Under the Precision-Recall Curve
    random_state=42
)

xgb_model.fit(X_train_smote, y_train_smote)

print("\n4. Evaluating the Model...")
# Predict probabilities on the UNBALANCED test set to mimic real-world evaluation
y_pred_proba = xgb_model.predict_proba(X_test)[:, 1]
y_pred = xgb_model.predict(X_test)

# Calculate AUC-PR explicitly to minimize False Positive Rate (FPR)[cite: 1]
auc_pr = average_precision_score(y_test, y_pred_proba)

print("Classification Report:")
print(classification_report(y_test, y_pred))
print(f"Area Under the Precision-Recall Curve (AUC-PR): {auc_pr:.4f}")

print("\n5. Saving Model for Production...")
# Save the model to a JSON file so the Flask microservice can load it
model_filename = "fraud_model.json"
xgb_model.save_model(model_filename)

# Save feature names for SHAP explainability in the Flask app
import json
with open("fraud_feature_names.json", "w") as f:
    json.dump(FEATURE_NAMES, f)

print(f"Success! Model securely saved as '{model_filename}'. Your Flask app will now use this for real-time predictions.")
