# Project Rules

## Password Policy Enforcement
When building or modifying ANY authentication, registration, or password change features, you MUST enforce the strict password policy below. 

### Regular Expression
Always use this exact regex for data annotations and frontend validation:
`^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&^#_.:,+-])[a-zA-Z\d@$!%*?&^#_.:,+-]{8,}$`

### Requirements
- Minimum 8 characters
- At least 1 lowercase letter
- At least 1 uppercase letter
- At least 1 number
- At least 1 special character
- **NO SPACES AND NO EMOJIS ALLOWED**

Make sure both the Backend ViewModel (`[RegularExpression]`) and Frontend JavaScript match this policy to avoid confusing user feedback (e.g. green ticks showing for invalid passwords).
