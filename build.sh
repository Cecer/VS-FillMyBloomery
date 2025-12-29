#!/usr/bin/env bash

set -o errexit;

configuration="${1:-Debug}";

project_dir="$(dirname "$(realpath "$0")")";
project_file="$(find "$project_dir" -mindepth 1 -maxdepth 1 -name "*.csproj")";
project_name="$(basename "$project_file")";
project_name="${project_name%%.csproj}";
secrets_file="$(realpath "$project_dir/../.secrets.env")";
version="$(jq --raw-output .version "$project_dir/assets/modinfo.json")";

printf "[BuildScript] Project information:\n  Path: %s\n  Name: %s\n  Version: %s\n" "$project_dir" "$project_name" "$version";

cd "$project_dir";

printf "[BuildScript] Syncing project file\n";
for tag in Version AssemblyVersion FileVersion; do
    if grep --quiet "<$tag>" "$project_file"; then
        sed --in-place "s|<$tag>.*</$tag>|<$tag>$version</$tag>|" "$project_file";
    fi
done

printf "[BuildScript] Building project\n";
dotnet build --configuration "$configuration";

# Push to my public nuget on release builds
if [[ "$configuration" == "Release" ]]; then
    printf "[BuildScript] Pushing nuget package\n";
    if [[ ! -v CECER_NEXUS_API_KEY ]]; then
        if [[ -f "$secrets_file" ]]; then
            # shellcheck disable=SC1090
            source "$secrets_file";
            if [[ ! -v CECER_NEXUS_API_KEY ]]; then
                printf "[BuildScript] Warning: No API key specified and no default was set in %s\n" "$secrets_file" >&2;
            else
                printf "[BuildScript] Using API key from %s\n" "$secrets_file";
            fi
        else
            printf "[BuildScript] Warning: No API key specified and %s was not found\n" "$secrets_file";
        fi
    else
        printf "[BuildScript] Using API key from CECER_NEXUS_API_KEY\n";
    fi

    dotnet pack --include-source --no-build;
    dotnet nuget push "$project_dir/bin/$configuration/Mods/Cecer.VintageStory.$project_name.$version.symbols.nupkg" --source "https://nexus.cecer1.com/repository/nuget-public/index.json" --api-key "$CECER_NEXUS_API_KEY"
fi


printf "[BuildScript] Assembling zip file\n";
assembled_dir="$project_dir/builds/$project_name/";
mkdir --parents --verbose "$assembled_dir";
rm --recursive --force --verbose "${assembled_dir:?}/"*;

cp --verbose "bin/$configuration/Mods/$project_name.dll"  "$assembled_dir/";
cp --verbose "bin/$configuration/Mods/$project_name.pdb"  "$assembled_dir/";
cp --recursive --verbose assets/* "$assembled_dir/";

mkdir --parents --verbose "$project_dir/builds/zips";

if [[ "$configuration" == "Release" ]]; then
    zip_file="$project_dir/builds/zips/$project_name-$version.zip";
else
    zip_file="$project_dir/builds/zips/$project_name-${configuration}.zip";
fi
pushd "$assembled_dir";
zip --recurse-paths "$zip_file" .;
popd;

printf "[BuildScript] Zip file saved to %s\n" "$zip_file";
printf "[BuildScript] Build complete!\n";
