#!/bin/bash

SEMVER_REGEX="^[vV]?(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(\\-[0-9A-Za-z-]+(\\.[0-9A-Za-z-]+)*)?(\\+[0-9A-Za-z-]+(\\.[0-9A-Za-z-]+)*)?$"

function validate-version {
  local version=$1
  if [[ "$version" =~ $SEMVER_REGEX ]]; then
    # if a second argument is passed, store the result in var named by $2
    if [[ "$#" -eq "2" ]]; then
      local major=${BASH_REMATCH[1]}
      local minor=${BASH_REMATCH[2]}
      local patch=${BASH_REMATCH[3]}
      local prere=${BASH_REMATCH[4]}
      local build=${BASH_REMATCH[6]}
      eval "$2=(\"$major\" \"$minor\" \"$patch\" \"$prere\" \"$build\")"
    else
      echo "$version"
    fi
  else
    error "version $version does not match the semantic version scheme '[v]X.Y.Z(-PRERELEASE)(+BUILD)'."
  fi
}

function error {
  echo -e "$1" >&2
  exit 1
}

function deploy {
  # Check for passed api key which is always needed
  if [[ ${PUSH_KEY} == "" ]]; then
      echo "Missing API-KEY to access NUGET feed ${FEED}" 1>&2
      echo "Please provide one as a global parameter e.g. export PUSH_KEY=xxxx" >&2
      exit 1
  fi
  
  cd ./Release || exit
  
  dotnet nuget push "*.nupkg" -k "${PUSH_KEY}" --skip-duplicate --source https://api.nuget.org/v3/index.json
}

function localnuget {
  local path=$1
  cd ./Release || exit  
  
  for i in *.nupkg; do
      [ -f "$i" ] || break
      nuget add "$i" -Source "$path"
  done
}

function usage {
  error "usage: ./pack.sh [v].X.Y.Z [deploy|local]"
  exit 1
}

if [[ "$#" -le 0 ]];then
    usage
else
    version=$1

    # standard-version set's version tag with v prefix. So this gets
    # set in the GitLab pipeline, make sure we don't push a v to NuGet.
    if [ "${version:0:1}" == "v" ]; then
      version=${version:1}
    fi
fi

validate-version "${version}" V

if [[ "$2" == "deploy" ]];then
  cd ./Build || exit  
  ./clean-all.sh
  cd ..
fi  


if [[ "$2" == "local" ]];then
  if [[ "$3" == "" ]];then
    echo "Please provide a local path where the local NuGet package should be stored (this also the path you add in Rider)."
    echo "this path can be anywhere on your system as long as it's exists."
    echo "./pack.sh vX.Y.Z local /path/to/local/nuget"
    exit 1
  fi
  localnugetpath=$3  
fi

packages=(
  "Mars.Common" 
  "Mars.Base" 
  "Mars.Core" 
  "Mars.Components" 
  "Mars.Interfaces"
  "Mars.IO" 
  "Mars.Numerics" 
  "Mars.Simulations" # <- bundles all Mars.* packages
  # SOH
  "Models/SOHDomain" 
  "Models/SOHMultimodalModel"
  "Models/SOHCarModel"
  "Models/SOHBicycleModel"
  "Models/SOHBusModel"
  "Models/SOHFerryModel"
  "Models/SOHTrainModel"
  "MARS.SOH" # <- bundles all SOH packages
)

for package in "${packages[@]}"; do
  echo "Packaging project $package versioned as ${version} ..."
  dotnet pack "${package}" -v q --nologo -c Release --force --include-symbols -p:SymbolPackageFormat=snupkg --include-source /p:Version="${version}" -o ./Release
done

if [[ "$2" == "deploy" ]];then
  deploy
fi  
if [[ "$2" == "local" ]];then
  localnuget "$localnugetpath"
fi  