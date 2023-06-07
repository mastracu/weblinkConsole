#!/bin/bash
# $1 = weblinkendpoint
echo "building container"
docker build -t $1 .
echo "starting container:"
docker run -d \
  -it \
  -p8085:8083 \
  --mount type=bind,source="$(pwd)"/../zebrafirmware,target=/App/firmware \
  $1