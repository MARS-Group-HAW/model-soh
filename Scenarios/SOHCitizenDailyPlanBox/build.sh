#!/usr/bin/env bash

# Build the application and all of its dependencies
dotnet publish -c Release -o out 

# Navigate into the output directory
cd ./out

# Build the container
docker build -t git.haw-hamburg.de:5005/mars/life/soh-evaluation:latest . 

# Maybe a login is required
docker login git.haw-hamburg.de:5005 
# Push to the registry
docker push git.haw-hamburg.de:5005/mars/life/soh-evaluation:latest 

cd .. 
echo Pushed new image to registry: 
echo git.haw-hamburg.de:5005/mars/life/soh-evaluation:latest

# Finished the upload