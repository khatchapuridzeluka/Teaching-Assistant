# Sandbox images

Student code never runs on the host. It runs in these images, started by the API's sandbox runner
(`src/TeachingAssistant.Infrastructure/Sandbox/DockerSandboxRunner.cs`) with no
network, a read-only file system, memory, CPU and process limits, and an unprivileged user.

## ta-java

`java/Dockerfile`: a Temurin JDK plus Checkstyle 14.4.0, PMD 7.29.0 and SpotBugs 4.10.5 (downloaded at build time,
checked against pinned SHA-256 sums). Student code is compiled with the newest JDK first; the older one is the fallback
for the rare submission that only compiles there (old preview features, removed APIs).

Build both from the repository root:

```bash
docker build -t ta-java:27 sandbox/java
```

```bash
docker build -t ta-java:21 --build-arg JDK_VERSION=21 sandbox/java
```

Inside the image:

| Path / command | What it is |
|---|---|
| `/work` | Working directory, owned by the `sandbox` user (uid 10001); the runner copies the student's files here |
| `javac`, `java` | The JDK of the image's tag |
| `checkstyle` | `java -jar /opt/checkstyle/checkstyle.jar`, e.g. `checkstyle -c /google_checks.xml src` |
| `pmd` | PMD 7, e.g. `pmd check -d src -R rulesets/java/quickstart.xml -f text` |
| `spotbugs` | SpotBugs, e.g. `spotbugs -textui out` on compiled classes |

The container's default command is `sleep infinity`: the runner starts it, copies files in, runs each step with
`docker exec` (compile, console tests, analysis) and removes it afterwards. Started like this, which is what the runner
will do:

```bash
docker run -d --hostname localhost --network none --read-only --tmpfs /work:rw,exec,size=64m,uid=10001,gid=10001,mode=0700 --tmpfs /tmp:rw,size=64m,mode=1777 --memory 512m --cpus 1 --pids-limit 128 --cap-drop ALL --security-opt no-new-privileges ta-java:27
```

- `--network none`: the code reaches nothing, not the internet and not the database.
- `--read-only` with `tmpfs` for `/work` and `/tmp`: only those two are writable, and they vanish with the container.
  Files go in as a tar stream (`tar -cf - . | docker exec -i <id> tar -xf - -C /work`), because `docker cp` does not
  write into a tmpfs.
- `--hostname localhost`: without a network the container's own name does not resolve, and SpotBugs then logs an
  `UnknownHostException` (its results are still right, but the noise would end up in the output).
- `--memory`, `--cpus`, `--pids-limit`: no memory or fork bombs. Each step also gets its own timeout from the runner.
- `--cap-drop ALL --security-opt no-new-privileges` and the `sandbox` user: nothing runs as root.

Checked on both images: a two-class program in a package compiles and runs with stdin, prints Georgian text
correctly, Checkstyle, PMD and SpotBugs report on it, and network access and writes outside `/work` and `/tmp` fail.

To upgrade a tool, change its version and SHA-256 build arguments in the Dockerfile; the build fails if a download
does not match its sum.
