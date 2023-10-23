FROM mcr.microsoft.com/dotnet/core/runtime:3.1

# Please run the publish command before
# dotnet publish -c Release -o out

# Afterwards run the docker build command with a respective docker tag suitable for git.haw-hamburg.de:5005 e.g.
# docker build git.haw-hamburg.de:5005/mars/life/soh-run-1:latest

# Then push the container towards the registry
# docker push git.haw-hamburg.de:5005/mars/life/soh-run-1:latest

# Maybe a login is required then execute 
# docker login git.haw-hamburg.de:5005 
# before ...

# Navigate to the deployment.yaml (kubernetes deployment descriptor) and adjust the image you've pushed.


COPY ./ ./

WORKDIR ./

ENTRYPOINT dotnet SOHModelStarter.dll 
