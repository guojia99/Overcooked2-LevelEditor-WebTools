"""Read-only bundle47 audit of meat Workable/CookableIngredient source contracts."""
import pathlib
import sys
import UnityPy

ROOT = pathlib.Path(__file__).resolve().parents[4]
env = UnityPy.load(str(ROOT / 'Assets/StreamingAssets/Windows' / (sys.argv[1] if len(sys.argv) > 1 else 'bundle47')))

def resolve(owner, pointer):
    if not pointer or not pointer.get('m_PathID'):
        return None
    return owner.assets_file.objects.get(pointer['m_PathID'])

def tree(obj):
    try:
        return obj.read_typetree()
    except Exception:
        return {}

def script_name(obj, data):
    script = resolve(obj, data.get('m_Script'))
    return tree(script).get('m_ClassName', '?') if script else '?'

for obj in env.objects:
    if obj.type.name != 'GameObject':
        continue
    data = tree(obj)
    name = data.get('m_Name', '')
    rows = []
    for link in data.get('m_Component', []):
        component = resolve(obj, link.get('component', link))
        if component is None or component.type.name != 'MonoBehaviour':
            continue
        fields = tree(component)
        kind = script_name(component, fields)
        if kind not in ('WorkableItem', 'CookableIngredient', 'CookingHandler', 'IngredientPropertiesComponent'):
            continue
        row = {'type': kind}
        for key in ('m_stages', 'm_cookingtime', 'm_stationType', 'm_cookingProgress'):
            if key in fields:
                row[key] = fields[key]
        for key in ('m_nextPrefab', 'm_ingredientOrderNode', 'm_cookingType'):
            target = resolve(component, fields.get(key))
            if target:
                row[key] = {'path_id': target.path_id, 'name': tree(target).get('m_Name', '')}
        rows.append(row)
    if rows and (any(word in name.lower() for word in ('meat', 'chicken', 'steak', 'sausage'))
                 or any(row['type'] == 'CookableIngredient' for row in rows)):
        print({'object': name, 'path_id': obj.path_id, 'components': rows})
