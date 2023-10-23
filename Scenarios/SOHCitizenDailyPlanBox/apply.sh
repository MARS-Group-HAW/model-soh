#!/usr/bin/env bash

#kubectl -n mars delete job $(kubectl -n mars get job | grep soh-evaluation | awk '{ print $1 }')

#kubectl -n mars delete pod $(kubectl -n mars get pods | grep soh-evaluation | awk '{ print $1 }')

kubectl -n mars apply -f job.yaml

#watch -n1 kubectl -n mars get po