import codecs
with codecs.open('Views/Product/Create.cshtml', 'r', 'utf-8', 'ignore') as f:
    content = f.read()

# 1. Add Quill CSS to head
if 'quill.snow.css' not in content:
    content = content.replace('</head>', '    <link href="https://cdn.quilljs.com/1.3.6/quill.snow.css" rel="stylesheet">\r\n</head>')

# 2. Add Description Field
target_cat = '<!-- 3. Storefront Category Dropdown -->'
desc_html = '''<!-- 4. Product Description -->
                        <div class="form-group" style="margin-top: 20px;">
                            <label asp-for="Description" class="field-label">Product Description:</label>
                            <div id="editor" style="height: 250px; background: white; border: 1px solid #E2E8F0; border-radius: 6px;"></div>
                            <input type="hidden" asp-for="Description" id="DescriptionInput" />
                            <span asp-validation-for="Description" class="field-validation-error"></span>
                        </div>
                        
                        <!-- 3. Storefront Category Dropdown -->'''
                        
if target_cat in content and '<!-- 4. Product Description -->' not in content:
    content = content.replace(target_cat, desc_html)

# 3. Add Quill JS and script
script_html = '''    <script src="https://cdn.quilljs.com/1.3.6/quill.js"></script>
    <script>
        var quill = new Quill('#editor', {
            theme: 'snow',
            modules: {
                toolbar: [
                    [{ 'header': [1, 2, 3, false] }],
                    ['bold', 'italic', 'underline', 'strike'],
                    [{ 'color': [] }, { 'background': [] }],
                    [{ 'list': 'ordered'}, { 'list': 'bullet' }],
                    ['link', 'image', 'video'],
                    ['clean']
                ]
            }
        });
        
        var form = document.querySelector('form');
        form.addEventListener('submit', function() {
            var descInput = document.querySelector('#DescriptionInput');
            descInput.value = quill.root.innerHTML;
        });
    </script>
    @await Html.PartialAsync("_ChatPartial")'''

if 'quill.js' not in content:
    content = content.replace('    @await Html.PartialAsync("_ChatPartial")', script_html)

with codecs.open('Views/Product/Create.cshtml', 'w', 'utf-8') as f:
    f.write(content)
print('Create.cshtml updated')
