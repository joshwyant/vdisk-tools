FROM microsoft/dotnet:2.1-sdk

COPY . /vdisk-tools
WORKDIR /vdisk-tools

RUN chmod 777 build.sh && ./build.sh

ENV PATH="/vdisk-tools/bin:${PATH}"

WORKDIR /root
