import pandas as pd
import numpy as np
import xgboost as xgb
from sklearn.model_selection import train_test_split
from sklearn.metrics import average_precision_score, classification_report
import json

print("1. Generating highly imbalanced synthetic e-commerce data (with Velocity features)...")
# Simulating 10,000 transactions where only ~2% are fraudulent
np.random.seed(42)
n_samples = 10000

# Features: transactionAmount, accountAgeDays, failedLoginAttempts, distanceFromShippingAddress
# NEW Velocity Features: transactions_last_10_mins, time_since_account_creation_seconds, device_ip_flags
X_legit = np.random.normal(
    loc=[120, 365, 0, 15, 0, 31536000, 0], 
    scale=[50, 100, 0, 10, 0.5, 100000, 0], 
    size=(9800, 7)
)
y_legit = np.zeros(9800) # 0 = Legitimate

# Fraudsters typically have high transaction amounts, new accounts, failed logins, high distance, and high velocity
X_fraud = np.random.normal(
    loc=[800, 5, 4, 500, 5, 432000, 1], 
    scale=[200, 10, 2, 100, 2, 10000, 0.5], 
    size=(200, 7)
)
y_fraud = np.ones(200) # 1 = Fraud

# Combine into a single dataset
X = np.vstack((X_legit, X_fraud))
y = np.hstack((y_legit, y_fraud))

# Ensure no negative values for realistic data
X = np.clip(X, 0, None)

# Split into training and testing sets (80% train, 20% test)
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42, stratify=y)

print(f"Training Data Shape: {X_train.shape}. Fraud cases: {sum(y_train)}")

print("\n2. Training the XGBoost Model with scale_pos_weight...")
# Define feature names for SHAP interpretability
FEATURE_NAMES = [
    "transactionAmount", 
    "accountAgeDays", 
    "failedLoginAttempts", 
    "distanceFromShippingAddress",
    "transactions_last_10_mins", 
    "time_since_account_creation_seconds", 
    "device_ip_flags"
]

# Calculate scale_pos_weight dynamically based on the training split
count_negative = sum(y_train == 0)
count_positive = sum(y_train == 1)
scale_weight = count_negative / count_positive
print(f"Calculated scale_pos_weight: {scale_weight:.2f} ({count_negative} legit / {count_positive} fraud)")

xgb_model = xgb.XGBClassifier(
    objective='binary:logistic',
    eval_metric='aucpr', # Prioritize Area Under the Precision-Recall Curve
    scale_pos_weight=scale_weight,
    random_state=42
)

xgb_model.fit(X_train, y_train)

print("\n3. Evaluating the Model...")
# Predict probabilities on the UNBALANCED test set to mimic real-world evaluation
y_pred_proba = xgb_model.predict_proba(X_test)[:, 1]
y_pred = xgb_model.predict(X_test)

# Calculate AUC-PR explicitly to minimize False Positive Rate (FPR)
auc_pr = average_precision_score(y_test, y_pred_proba)

print("Classification Report:")
print(classification_report(y_test, y_pred))
print(f"Area Under the Precision-Recall Curve (AUC-PR): {auc_pr:.4f}")

print("\n4. Saving Model for Production...")
# Save the model to a JSON file so the Flask microservice can load it
model_filename = "fraud_model.json"
xgb_model.save_model(model_filename)

# Save feature names for SHAP explainability in the Flask app
with open("fraud_feature_names.json", "w") as f:
    json.dump(FEATURE_NAMES, f)

print(f"Success! Model securely saved as '{model_filename}'. Your Flask app will now use this for real-time predictions.")
