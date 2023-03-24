#!/bin/bash
# $1 = weblinkendpoint
echo "building container: docker build -t $1 ."
docker build -t $1 .
echo "stopping all containers"
docker stop $(docker ps -a -q)
echo "removing stopped containers"
docker container prune -f
echo "removing all untagged images"
docker images --no-trunc | grep '<none>' | awk '{ print $3 }'  | xargs -r docker rmi
echo "starting container:"
docker run -d \
  -it \
  -p8084:8083 \
  --mount type=bind,source="$(pwd)"/../zebrafirmware,target=/App/firmware \
  $1