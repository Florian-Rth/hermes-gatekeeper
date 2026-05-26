FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends openssh-server procps \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /bin/sh gatekeeper-readonly \
    && passwd -d gatekeeper-readonly \
    && mkdir -p /var/run/sshd /home/gatekeeper-readonly/.ssh

COPY ssh_host_ed25519_key /etc/ssh/ssh_host_ed25519_key
COPY ssh_host_ed25519_key.pub /etc/ssh/ssh_host_ed25519_key.pub
COPY authorized_keys /home/gatekeeper-readonly/.ssh/authorized_keys
COPY gatekeeper-demo-command /usr/local/bin/gatekeeper-demo-command
COPY sshd_config /etc/ssh/sshd_config

RUN chown root:root /etc/ssh/ssh_host_ed25519_key /etc/ssh/ssh_host_ed25519_key.pub /etc/ssh/sshd_config \
    && chmod 600 /etc/ssh/ssh_host_ed25519_key \
    && chmod 644 /etc/ssh/ssh_host_ed25519_key.pub /etc/ssh/sshd_config \
    && chown root:root /usr/local/bin/gatekeeper-demo-command \
    && chmod 755 /usr/local/bin/gatekeeper-demo-command \
    && chown -R gatekeeper-readonly:gatekeeper-readonly /home/gatekeeper-readonly/.ssh \
    && chmod 700 /home/gatekeeper-readonly/.ssh \
    && chmod 600 /home/gatekeeper-readonly/.ssh/authorized_keys

EXPOSE 22

CMD ["/usr/sbin/sshd", "-D", "-e"]
