# syntax=docker/dockerfile:1
#
# The sandbox image student Java code runs in: a JDK plus Checkstyle, PMD, SpotBugs and the JUnit runner, nothing else.
# Build one image per JDK (see sandbox/README.md): ta-java:27 first, ta-java:21 as the fallback.
#
# The image needs no network at run time. The runner starts it with --network none, a read-only root file system,
# a small writable /work, memory/CPU/process limits and the unprivileged sandbox user, then runs each step with
# docker exec. Nothing here should print to stdout or stderr on its own (no JAVA_TOOL_OPTIONS, which the JVM echoes),
# because the console tests compare that output.

ARG JDK_VERSION=27

# ---- Tools: downloaded and checked once, at build time ----
FROM eclipse-temurin:${JDK_VERSION}-jdk AS tools

ARG CHECKSTYLE_VERSION=14.4.0
ARG CHECKSTYLE_SHA256=c0a521e54eabd1ece4ff7bd84cf231b012615745ed3aa4fb27ab0ca73097250c
ARG PMD_VERSION=7.29.0
ARG PMD_SHA256=d27177c185b837fa57f097e482d1253468aa035379c5eb2671fa0a279109b179
ARG SPOTBUGS_VERSION=4.10.5
ARG SPOTBUGS_SHA256=cce3244900cc215ccd9f5a8db29ed9468fc47824faf662f4fad005343d96007e
ARG JUNIT_VERSION=6.1.3
ARG JUNIT_SHA256=e62b96ac475dbcde8599ea905d088f65d90778f86e259b856a49fa5c4ea256ec

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl unzip \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /download

RUN curl -fsSL -o checkstyle.jar \
      "https://github.com/checkstyle/checkstyle/releases/download/checkstyle-${CHECKSTYLE_VERSION}/checkstyle-${CHECKSTYLE_VERSION}-all.jar" \
 && echo "${CHECKSTYLE_SHA256}  checkstyle.jar" | sha256sum -c - \
 && mkdir -p /opt/checkstyle \
 && mv checkstyle.jar /opt/checkstyle/checkstyle.jar

RUN curl -fsSL -o pmd.zip \
      "https://github.com/pmd/pmd/releases/download/pmd_releases%2F${PMD_VERSION}/pmd-dist-${PMD_VERSION}-bin.zip" \
 && echo "${PMD_SHA256}  pmd.zip" | sha256sum -c - \
 && unzip -q pmd.zip \
 && mv "pmd-bin-${PMD_VERSION}" /opt/pmd \
 && rm pmd.zip

RUN curl -fsSL -o spotbugs.tgz \
      "https://github.com/spotbugs/spotbugs/releases/download/${SPOTBUGS_VERSION}/spotbugs-${SPOTBUGS_VERSION}.tgz" \
 && echo "${SPOTBUGS_SHA256}  spotbugs.tgz" | sha256sum -c - \
 && tar -xzf spotbugs.tgz \
 && mv "spotbugs-${SPOTBUGS_VERSION}" /opt/spotbugs \
 && rm spotbugs.tgz

# The JUnit console runner with JUnit Jupiter in one jar: the tests generated for an assignment compile against it and
# run with it.
RUN curl -fsSL -o junit.jar \
      "https://repo1.maven.org/maven2/org/junit/platform/junit-platform-console-standalone/${JUNIT_VERSION}/junit-platform-console-standalone-${JUNIT_VERSION}.jar" \
 && echo "${JUNIT_SHA256}  junit.jar" | sha256sum -c - \
 && mkdir -p /opt/junit \
 && mv junit.jar /opt/junit/junit.jar

# ---- The sandbox ----
FROM eclipse-temurin:${JDK_VERSION}-jdk

ARG JDK_VERSION
LABEL org.opencontainers.image.title="ta-java" \
      org.opencontainers.image.description="Sandbox for compiling, running and checking student Java code (JDK ${JDK_VERSION})"

COPY --from=tools /opt/checkstyle /opt/checkstyle
COPY --from=tools /opt/pmd /opt/pmd
COPY --from=tools /opt/spotbugs /opt/spotbugs
COPY --from=tools /opt/junit /opt/junit

# Short commands for the runner's steps: checkstyle, pmd and spotbugs, each passing its arguments through.
RUN printf '#!/bin/sh\nexec java -jar /opt/checkstyle/checkstyle.jar "$@"\n' > /usr/local/bin/checkstyle \
 && ln -s /opt/pmd/bin/pmd /usr/local/bin/pmd \
 && chmod +x /opt/spotbugs/bin/spotbugs \
 && ln -s /opt/spotbugs/bin/spotbugs /usr/local/bin/spotbugs \
 && chmod +x /usr/local/bin/checkstyle

# An unprivileged user for student code (uid 1000 is the base image's ubuntu user, so use another).
RUN groupadd --gid 10001 sandbox \
 && useradd --uid 10001 --gid sandbox --no-create-home --home-dir /work --shell /usr/sbin/nologin sandbox \
 && mkdir -p /work \
 && chown sandbox:sandbox /work

# UTF-8 everywhere, so Georgian text in console output reads the same as on the student's machine (Java 18+ default).
ENV LANG=C.UTF-8 \
    LC_ALL=C.UTF-8 \
    TZ=UTC

USER sandbox
WORKDIR /work

# Keeps the container alive while the runner copies files in and runs its steps with docker exec.
CMD ["sleep", "infinity"]
