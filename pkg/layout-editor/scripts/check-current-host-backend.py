#!/usr/bin/env python3
"""Compile current Stub, full game/LevelEditor host and standalone backend, C#4/net35.

No old Assembly-CSharp.dll or LevelEditorStub.dll is referenced. Generated projects
and binaries live in a temporary directory; no Unity process or scene is touched.
"""
import argparse
from contextlib import nullcontext
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-managed', type=Path, default=Path(
        '/Applications/Unity/Hub/Editor/2017.4.8f1/Unity.app/Contents/Managed'))
    parser.add_argument('--backend-core', action='store_true',
                        help='Exclude HTTP transport only, with an explicit API declaration double')
    parser.add_argument('--output-dir', type=Path,
                        help='Keep generated projects, logs and binaries in a new directory for review')
    parser.add_argument('--inspectors', action='store_true',
                        help='Also compile the two upstream recipe/category inspectors independently')
    args = parser.parse_args()
    unity = args.unity_managed
    if not unity.is_dir():
        unity = ROOT / 'layout-editor/offline-check/bin/standalone'
    engine = unity / 'UnityEngine' if (unity / 'UnityEngine').is_dir() else unity
    refs = sorted(engine.glob('UnityEngine*.dll')) + [unity / 'UnityEditor.dll']
    assert len(refs) > 10 and all(p.is_file() for p in refs), 'Unity 2017 Managed DLLs required'
    package_root = Path(os.environ.get('NUGET_PACKAGES', str(Path.home() / '.nuget/packages')))
    frameworks = sorted(package_root.glob('microsoft.netframework.referenceassemblies.net35/*/build/.NETFramework/v3.5'))
    assert frameworks, 'Cached net35 reference assemblies required'
    # Only Unity and third-party plugins are binary inputs. Rebuild every host assembly.
    host_project = ET.parse(ROOT / 'Assembly-CSharp.csproj').getroot()
    ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
    for node in host_project.findall('.//m:Reference/m:HintPath', ns):
        p = Path(node.text.replace('\\', '/'))
        if p.is_file() and p.name not in ('Assembly-CSharp.dll', 'LevelEditorStub.dll'):
            if not p.name.startswith('UnityEngine') and p.name != 'UnityEditor.dll':
                refs.append(p)
    refs += sorted((ROOT / 'Assets/Plugins').glob('*.dll'))
    # Resolve Unity extension assemblies from the installed editor, even if the csproj is stale.
    if (unity.parent / 'UnityExtensions').is_dir():
        refs += sorted((unity.parent / 'UnityExtensions').glob('**/UnityEngine*.dll'))
    unique = {}
    for p in refs:
        if p.is_file():
            unique.setdefault(p.name, p)
    refs = list(unique.values())
    assert all('Library/ScriptAssemblies' not in str(p) for p in refs), 'Old host binaries must not enter reference set'
    assert not any(p.name in ('Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll', 'LevelEditorStub.dll')
                   for p in refs), 'Host references must be compiled from current sources'
    if args.output_dir:
        args.output_dir = args.output_dir.resolve()
        args.output_dir.mkdir(parents=False, exist_ok=False)
    workspace = nullcontext(str(args.output_dir)) if args.output_dir else tempfile.TemporaryDirectory(prefix='oc2-current-host-')
    failures = []
    with workspace as temp:
        out = Path(temp)
        print('CHECK PROJECT ROOT: ' + str(out), flush=True)
        (out / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')

        def build(name, sources, references):
            folder = out / name
            folder.mkdir()
            project = folder / (name + '.csproj')
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<TargetFramework>net35</TargetFramework><LangVersion>4</LangVersion>'
                '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                '<AutomaticallyUseReferenceAssemblyPackages>false</AutomaticallyUseReferenceAssemblyPackages>'
                '<FrameworkPathOverride>' + escape(str(frameworks[-1])) + '</FrameworkPathOverride>'
                '<GenerateAssemblyInfo>false</GenerateAssemblyInfo><NuGetAudit>false</NuGetAudit>'
                '<AllowUnsafeBlocks>true</AllowUnsafeBlocks><DefineConstants>UNITY_EDITOR;UNITY_2017_4;UNITY_2017_4_OR_NEWER</DefineConstants>'
                '<NoWarn>0649;0169;0618;0168</NoWarn></PropertyGroup><ItemGroup>'
                + ''.join('<Compile Include="' + escape(str(p)) + '" />' for p in sources)
                + ''.join('<Reference Include="' + escape(str(p)) + '" />' for p in references)
                + '</ItemGroup></Project>', encoding='utf-8')
            result = subprocess.run(['dotnet', 'build', str(project), '--nologo', '-v:q'],
                                    cwd=out, env=env, capture_output=True, text=True, timeout=180)
            (folder / 'build.log').write_text(result.stdout + result.stderr, encoding='utf-8')
            if result.returncode:
                failures.append(name)
                print('FAIL ' + name + '; project: ' + str(project), flush=True)
                print(result.stdout + result.stderr, flush=True)
                return None
            print('PASS ' + name + ': C#4/net35, ' + str(len(sources)) + ' current source files', flush=True)
            return folder / 'bin/Debug/net35' / (name + '.dll')

        firstpass = build('Assembly-CSharp-firstpass', sorted((ROOT / 'Assets/Plugins/Assembly-CSharp-firstpass').glob('**/*.cs')), refs)
        if firstpass is None:
            raise SystemExit('Firstpass failed; dependent builds blocked. Projects retained only with --output-dir.')
        refs.append(firstpass)
        stub = build('LevelEditorStub', sorted((ROOT / 'Assets/Scripts/LevelEditorStub').glob('**/*.cs')), refs)
        if stub is None:
            raise SystemExit('Stub failed; dependent builds blocked. Projects retained only with --output-dir.')
        if args.inspectors:
            for name in ('CustomRecipeSOEditor', 'PseudoPrefabSOCategoryDrawer'):
                source = ROOT / 'Assets/Editor' / (name + '.cs')
                assert source.is_file(), 'Missing inspector: ' + str(source)
                build(name + 'Check', [source], refs + [stub])
        host_sources = sorted((ROOT / 'Assets/Scripts/Assembly-CSharp').glob('**/*.cs'))
        host_sources += sorted((ROOT / 'Assets/Scripts/LevelEditor').glob('**/*.cs'))
        host_sources += sorted((ROOT / 'Assets/Modules').glob('*/Scripts/**/*.cs'))
        host = build('Assembly-CSharp', host_sources, refs + [stub])
        if host is None:
            raise SystemExit('FAILED: ' + ', '.join(failures) + '; backend blocked by host compile failure.')
        backend = sorted((ROOT / 'Assets/Editor/LayoutEditor').glob('**/*.cs'))
        if args.backend_core:
            backend = [p for p in backend if p.name != 'LayoutEditorHttpServer.cs']
            shim = out / 'HttpTransportDeclaration.cs'
            shim.write_text('public class LayoutEditorHttpServer {'
                'public const int DefaultPort=8765; public bool IsRunning; public bool IsHealthy; public int Port;'
                'public static System.Func<string,string,string> CustomStubApi;'
                'public static System.Func<string,string> CustomStubWriteBackCheck;'
                'public bool Start(int port=8765){return false;} public void Stop(){}'
                'public static bool HasBundledWebUi(){return false;} }')
            backend.append(shim)
            print('CORE ONLY: HTTP implementation excluded; declaration double does not validate HTTP.', flush=True)
        build('LayoutEditorCurrentCheck', backend, refs + [stub, host])
    if failures:
        raise SystemExit('FAILED: ' + ', '.join(failures))
    print('No Unity execution or scene/asset writes; no Library/ScriptAssemblies DLLs were referenced.', flush=True)


if __name__ == '__main__':
    main()
