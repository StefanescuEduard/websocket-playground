const WebSocket = require('ws');

const PORT = 5002;

const reaWebSocket = new WebSocket.Server({ port: PORT });

reaWebSocket.on('connection', (socket) => {
  console.log("A client just connected...");

  reaWebSocket.clients.forEach(function (client) {
  
    setInterval(() => {
      client.send("[\"/provisioning\", \"/servers\"]");
    }, 1000);
  });

  socket.on('message', (rawData) => {
    console.log(String(rawData));
  });
});

console.log(`[${new Date().toLocaleString()}] server is listening on port ${PORT}...`);
