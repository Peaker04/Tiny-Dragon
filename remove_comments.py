import re

def remove_comments(file_path):
    with open(file_path, 'r', encoding='utf-8-sig') as f:
        content = f.read()

    # First, remove lines that only contain comments (including leading whitespace)
    # This matches optional spaces, followed by // (which covers /// too), to the end of the line, and the newline
    content = re.sub(r'^[ \t]*\/\/[^\n]*\r?\n', '', content, flags=re.MULTILINE)

    # Next, remove inline comments or block comments, but preserve string literals
    pattern = r'(@\"(?:[^\"]|\"\")*\"|\"(?:\\.|[^\"\\])*\")|(\/\/[^\n]*|\/\*[\s\S]*?\*\/)'
    
    def replacer(match):
        if match.group(2) is not None:
            return '' # Remove the comment
        else:
            return match.group(1) # Keep the string

    content = re.sub(pattern, replacer, content)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)

files = [
    r'd:\IT\SU26\PRU213\Tiny-Dragon_2\Assets\_Project\Scripts\UI\SettingsManager.cs',
    r'd:\IT\SU26\PRU213\Tiny-Dragon_2\Assets\_Project\Scripts\UI\PauseManager.cs',
    r'd:\IT\SU26\PRU213\Tiny-Dragon_2\Assets\_Project\Scripts\UI\IntroManager.cs',
    r'd:\IT\SU26\PRU213\Tiny-Dragon_2\Assets\_Project\Scripts\UI\InventoryPanel.cs',
    r'd:\IT\SU26\PRU213\Tiny-Dragon_2\Assets\_Project\Scripts\Data\InventoryViewData.cs'
]

for f in files:
    remove_comments(f)
print('Done!')
