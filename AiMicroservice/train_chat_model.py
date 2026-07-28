import pandas as pd
import joblib
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, accuracy_score

print("1. Downloading the real UCI SMS Spam Collection dataset from the internet...")

# Download the dataset directly via pandas using a raw URL
url = "https://raw.githubusercontent.com/justmarkham/pycon-2016-tutorial/master/data/sms.tsv"
df = pd.read_csv(url, sep='\t', header=None, names=['label', 'message'])

print(f"Dataset loaded successfully! Total real messages: {len(df)}")

# The dataset uses 'ham' for safe messages and 'spam' for malicious ones.
# We map these to 0 (Safe) and 1 (Malicious) for our machine learning model.
df['label'] = df['label'].map({'ham': 0, 'spam': 1})

print("2. Vectorizing text using TF-IDF (Building vocabulary)...")
# TfidfVectorizer converts text into a mathematical matrix of word frequencies
# We increased max_features to 3000 to capture a wider vocabulary from the real data
vectorizer = TfidfVectorizer(stop_words='english', max_features=3000)
X = vectorizer.fit_transform(df['message'])
y = df['label']

print("3. Training the Random Forest NLP Classifier on Real Data...")
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

clf = RandomForestClassifier(n_estimators=100, random_state=42)
clf.fit(X_train, y_train)

print("\n4. Evaluating the Chat NLP Model...")
y_pred = clf.predict(X_test)
print(f"Accuracy: {accuracy_score(y_test, y_pred) * 100:.2f}%\n")
print(classification_report(y_test, y_pred, target_names=["Safe", "Malicious/Spam"]))

print("5. Exporting Model and Vectorizer for Production...")
# Save BOTH the model and the exact vocabulary vectorizer
joblib.dump(vectorizer, "chat_tfidf_vectorizer.pkl")
joblib.dump(clf, "chat_nlp_model.pkl")

print("Success! Real-world NLP model and vectorizer securely saved.")
