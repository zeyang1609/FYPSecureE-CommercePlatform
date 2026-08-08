 = 'c:\Users\zeyang\Desktop\FYP\Views\Product\Create.cshtml'
 = Get-Content  -Raw
 = '                        <!-- 2. Price & Stock Row -->
                        <div class="form-row">
                            <div class="form-group">
                                <label asp-for="Price" class="field-label">Price (RM):</label>
                                <div class="input-wrapper">
                                    <input asp-for="Price" type="number" step="0.01" min="0.01" class="mvc-input" placeholder="0.00" required />
                                </div>
                                <span asp-validation-for="Price" class="field-validation-error"></span>
                            </div>

                            <div class="form-group">
                                <label asp-for="StockLevel" class="field-label">Stock Quantity:</label>
                                <div class="input-wrapper">
                                    <input asp-for="StockLevel" type="number" min="1" class="mvc-input" placeholder="e.g., 50" required />
                                </div>
                                <span asp-validation-for="StockLevel" class="field-validation-error"></span>
                            </div>
                        </div>'

 = '                        <!-- 2. Price, Stock, & Weight Row -->
                        <div class="form-row">
                            <div class="form-group">
                                <label asp-for="Price" class="field-label">Price (RM):</label>
                                <div class="input-wrapper">
                                    <input asp-for="Price" type="number" step="0.01" min="0.01" class="mvc-input" placeholder="0.00" required />
                                </div>
                                <span asp-validation-for="Price" class="field-validation-error"></span>
                            </div>

                            <div class="form-group">
                                <label asp-for="StockLevel" class="field-label">Stock Quantity:</label>
                                <div class="input-wrapper">
                                    <input asp-for="StockLevel" type="number" min="1" class="mvc-input" placeholder="e.g., 50" required />
                                </div>
                                <span asp-validation-for="StockLevel" class="field-validation-error"></span>
                            </div>

                            <div class="form-group">
                                <label asp-for="WeightKg" class="field-label">Weight (kg):</label>
                                <div class="input-wrapper">
                                    <input asp-for="WeightKg" type="number" step="0.01" min="0.01" class="mvc-input" placeholder="e.g., 1.50" required />
                                </div>
                                <span asp-validation-for="WeightKg" class="field-validation-error"></span>
                            </div>
                        </div>'

 = .Replace("
", "
").Replace("
", "
")
 = .Replace("
", "
").Replace("
", "
")
 = .Replace("
", "
").Replace("
", "
")

if (.Contains()) {
     = .Replace(, )
    [IO.File]::WriteAllText(, , [System.Text.Encoding]::UTF8)
    Write-Host 'Success'
} else {
    Write-Host 'Target not found'
}
