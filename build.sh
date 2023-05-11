#!/bin/sh

docker build . -t talkingbot:latest
docker image push pi4er/talkingbot:latest