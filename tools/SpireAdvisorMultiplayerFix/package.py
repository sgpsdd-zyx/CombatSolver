import argparse
import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


MOD = "\u5c16\u5854\u519b\u5e08"
FEATURES = "\u529f\u80fd\u8bf4\u660e.txt"
INSTALL = "\u5b89\u88c5\u8bf4\u660e.txt"
NOTES = "\u4fee\u590d\u8bf4\u660e.txt"
VERSION = "0.1.3"


def main():
    parser = argparse.ArgumentParser(description="Package the tested local-player fix without game dependencies.")
    parser.add_argument("original_directory", type=Path)
    parser.add_argument("patched_dll", type=Path)
    parser.add_argument("output_zip", type=Path)
    args = parser.parse_args()

    tool_directory = Path(__file__).resolve().parent
    repository = tool_directory.parent.parent
    manifest = json.loads((args.original_directory / MOD / (MOD + ".json")).read_text(encoding="utf-8-sig"))
    if manifest["id"] != MOD or manifest["version"] != "v0.1.0":
        raise ValueError("The input manifest differs from the reviewed v0.1.2 archive.")
    manifest["version"] = "v" + VERSION
    features = (args.original_directory / FEATURES).read_text(encoding="utf-8-sig").replace("v0.1.0", "v" + VERSION)
    files = {
        MOD + "/" + MOD + ".dll": args.patched_dll.read_bytes(),
        MOD + "/" + MOD + ".json": (json.dumps(manifest, ensure_ascii=False, indent=2) + "\n").encode("utf-8"),
        MOD + "/LICENSE": (repository / "LICENSE").read_bytes(),
        MOD + "/THIRD_PARTY_NOTICES.md": (repository / "THIRD_PARTY_NOTICES.md").read_bytes(),
        FEATURES: features.encode("utf-8"),
        INSTALL: (tool_directory / "INSTALL.txt").read_bytes(),
        NOTES: (tool_directory / "RELEASE_NOTES.md").read_bytes(),
    }
    args.output_zip.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(args.output_zip, "x", compression=ZIP_DEFLATED) as archive:
        for name, content in files.items():
            archive.writestr(name, content)
    print("PACKAGED version=" + VERSION + " files=" + str(len(files)) + " output=" + str(args.output_zip.resolve()))


if __name__ == "__main__":
    main()
