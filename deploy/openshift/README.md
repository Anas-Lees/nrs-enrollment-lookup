# Deploying to OpenShift

The CI/CD pipeline ([`.github/workflows/cd.yml`](../../.github/workflows/cd.yml)) builds and
pushes the API and SPA images to GHCR on every push to `main`. Applying these manifests to a
cluster is a **manual step** in this POC (no cluster is wired up). When you have a cluster:

## 1. Log in and select a project

```bash
oc login <your-openshift-api-url> --token=<your-token>
oc new-project nrs   # or: oc project <existing>
```

## 2. Make the GHCR images pullable

The images are published to `ghcr.io/anas-lees/nrs-enrollment-lookup-{api,spa}`. If the packages
are private, create a pull secret and link it:

```bash
oc create secret docker-registry ghcr \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<gh-pat>
oc secrets link default ghcr --for=pull
```

## 3. Apply the manifests

```bash
oc apply -f deploy/openshift/
oc rollout status deployment/nrs-api --timeout=180s
oc rollout status deployment/nrs-spa --timeout=180s
oc get route nrs-spa     # the public HTTPS URL
```

## Re-enabling automated deploy

To deploy from CI, add a repository **variable** `OPENSHIFT_SERVER` and **secret**
`OPENSHIFT_TOKEN`, then add a `deploy` job to `cd.yml` (gated on
`if: ${{ vars.OPENSHIFT_SERVER != '' }}`) that runs the `oc login` / `oc apply` steps above.
