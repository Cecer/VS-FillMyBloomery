#!/usr/bin/env bash

set -o errexit;

project_dir="$(dirname "$(realpath "$0")")";
project_file="$(find "$project_dir" -mindepth 1 -maxdepth 1 -name "*.csproj")";
project_name="$(basename "$project_file")";
project_name="${project_name%%.csproj}";
src_path="$project_dir/builds/$project_name";
dest_path="/home/chris/.var/app/at.vintagestory.VintageStory/config/VintagestoryData/Mods/$project_name";

is_mounted() {
    path="$1";
    cat /proc/mounts | cut -d " " -f 2 | grep "^$path\$" > /dev/null
}

if is_mounted "$dest_path"; then
    printf "Already mounted!" >&2;
    exit 1;
fi

[[ -d "$dest_path" ]] || mkdir "$dest_path";

"$project_dir/build.sh";
sudo mount --bind "$src_path" "$dest_path";
printf "Bind mounted %s at %s\n" "$src_path" "$dest_path";
