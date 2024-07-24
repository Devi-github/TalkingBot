#!/bin/sh

docker build . -t pi4er/talkingbot:nightly
docker push pi4er/talkingbot:nightly
