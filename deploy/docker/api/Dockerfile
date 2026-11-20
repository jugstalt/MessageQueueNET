FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

#
# Container Security
# https://techcommunity.microsoft.com/t5/azure-developer-community-blog/hardening-an-asp-net-container-running-on-kubernetes/ba-p/2542224
#
# not 80 => errors with RunAsNonRoot
EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:8080
# readOnlyRootFilesystem = true works with this:
ENV DOTNET_EnableDiagnostics=0

WORKDIR /app
COPY app/ .
# optional files (e.g. _config/*.json) that override the published app
COPY override/ .

# default persist location (writable for the non-root user, also as volume mount point)
# used by the first start setup instead of /etc/message-queue
ENV MESSAGEQUEUE_DATA_PATH=/home/app/messagequeue
RUN mkdir -p /home/app/messagequeue && chown -R $APP_UID /home/app/messagequeue

USER $APP_UID

ENTRYPOINT ["dotnet", "MessageQueueNET.dll"]
