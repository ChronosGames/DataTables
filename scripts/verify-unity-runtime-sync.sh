#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_dir="$repo_root/src/DataTables"
unity_dir="$repo_root/src/DataTables.Unity/Assets/Scripts/DataTables"

if [[ ! -d "$source_dir" ]]; then
  echo "Source directory not found: $source_dir" >&2
  exit 1
fi

if [[ ! -d "$unity_dir" ]]; then
  echo "Unity mirror directory not found: $unity_dir" >&2
  exit 1
fi

tmp_source="$(mktemp)"
tmp_expected="$(mktemp)"
tmp_unity="$(mktemp)"
trap 'rm -f "$tmp_source" "$tmp_expected" "$tmp_unity"' EXIT

map_unity_path() {
  local relative_path="$1"
  local file_name="${relative_path##*/}"
  case "$file_name" in
    LRUDataTableCache.cs)
      printf './Runtime/Caching/%s\n' "$file_name"
      ;;
    DataSourceDecorators.cs|FileSystemDataSource.cs|HashValidatingReadStream.cs|IDataSource.cs|NetworkDataSource.cs|OwnedReadStream.cs|UnityDataSourceAdapters.cs)
      printf './Runtime/Sources/%s\n' "$file_name"
      ;;
    DataTableBinaryFormat.cs|DataTableBinaryLoader.cs|DataTableBinaryProtocol.cs)
      printf './Runtime/Protocol/%s\n' "$file_name"
      ;;
    *)
      printf './Runtime/Core/%s\n' "$relative_path"
      ;;
  esac
}

(
  cd "$source_dir"
  find . -type f -name '*.cs' \
    ! -path './bin/*' \
    ! -path './obj/*' \
    ! -name '_InternalVisibleTo.cs' \
    | sort
) > "$tmp_source"

while IFS= read -r relative_path; do
  map_unity_path "${relative_path#./}"
done < "$tmp_source" | sort > "$tmp_expected"

(
  cd "$unity_dir"
  find . -type f -name '*.cs' | sort
) > "$tmp_unity"

if ! diff -u "$tmp_expected" "$tmp_unity"; then
  echo "Unity runtime mirror file list differs from src/DataTables." >&2
  echo "Build src/DataTables to regenerate src/DataTables.Unity/Assets/Scripts/DataTables, then commit the synchronized files." >&2
  exit 1
fi

while IFS= read -r relative_path; do
  relative_path="${relative_path#./}"
  unity_path="$(map_unity_path "$relative_path")"
  unity_path="${unity_path#./}"
  if ! cmp -s "$source_dir/$relative_path" "$unity_dir/$unity_path"; then
    echo "Unity runtime mirror content differs: $relative_path" >&2
    echo "Source: $source_dir/$relative_path" >&2
    echo "Unity : $unity_dir/$unity_path" >&2
    exit 1
  fi
done < "$tmp_source"

echo "Unity runtime mirror is synchronized with src/DataTables."
