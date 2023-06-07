#!/bin/bash
# $1 = weblinkendpoint
echo "building container"
docker build -t $1 .
echo "starting newly created container:"
docker run -d \
  -it \
  -p8084:8083 \
  --mount type=bind,source="$(pwd)"/../zebrafirmware,target=/App/firmware \
  $1