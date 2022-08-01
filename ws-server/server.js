const WebSocket = require('ws');
const http = require('http')
const url = require('url')

const PORT = 5002;

const server = http.createServer();
const provisioningServer = new WebSocket.Server({ noServer: true });
const agentVersionServer = new WebSocket.Server({ noServer: true });

provisioningServer.on('connection', (socket) => {
  console.log("A client just connected...");

  sendData(socket, "some provisioning data", 1000);

  socket.on('message', (rawData) => {
    console.log(String(rawData));
  });
});

agentVersionServer.on('connection', (socket) => {
  console.log("A client just connected...");

  sendData(socket, "some agent version data", 2000);

  socket.on('message', (rawData) => {
    console.log(String(rawData));
  });
});

server.on('upgrade', (request, socket, head) => {
  const pathname = url.parse(request.url).pathname;

  if (pathname === '/provisioning') {
    provisioningServer.handleUpgrade(request, socket, head, (ws) => {
      provisioningServer.emit('connection', ws);
    });
  } else if (pathname === '/agentVersion') {
    agentVersionServer.handleUpgrade(request, socket, head, (ws) => {
      agentVersionServer.emit('connection', ws);
    });
  } else {
    socket.destroy();
  }
});

server.listen(PORT);

console.log(`[${new Date().toLocaleString()}] server is listening on port ${PORT}...`);

function sendData(socket, data, interval) {
  setInterval(() => {
    socket.send(data);
  }, interval);
}
