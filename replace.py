import codecs
with codecs.open('Views/Product/Create.cshtml', 'r', 'utf-8', 'ignore') as f:
    content = f.read()

target = '''<div class="form-group">\r\n                                <label asp-for="StockLevel" class="field-label">Stock Quantity:</label>\r\n                                <div class="input-wrapper">\r\n                                    <input asp-for="StockLevel" type="number" min="1" class="mvc-input" placeholder="e.g., 50" required />\r\n                                </div>\r\n                                <span asp-validation-for="StockLevel" class="field-validation-error"></span>\r\n                            </div>'''

replacement = '''<div class="form-group">\r\n                                <label asp-for="StockLevel" class="field-label">Stock Quantity:</label>\r\n                                <div class="input-wrapper">\r\n                                    <input asp-for="StockLevel" type="number" min="1" class="mvc-input" placeholder="e.g., 50" required />\r\n                                </div>\r\n                                <span asp-validation-for="StockLevel" class="field-validation-error"></span>\r\n                            </div>\r\n\r\n                            <div class="form-group">\r\n                                <label asp-for="WeightKg" class="field-label">Weight (kg):</label>\r\n                                <div class="input-wrapper">\r\n                                    <input asp-for="WeightKg" type="number" step="0.01" min="0.01" class="mvc-input" placeholder="e.g., 1.50" required />\r\n                                </div>\r\n                                <span asp-validation-for="WeightKg" class="field-validation-error"></span>\r\n                            </div>'''

if target in content:
    content = content.replace(target, replacement)
    with codecs.open('Views/Product/Create.cshtml', 'w', 'utf-8') as f:
        f.write(content)
    print('Success')
else:
    print('Target not found')
