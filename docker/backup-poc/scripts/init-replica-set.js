try {
  const status = rs.status();

  if (status.ok === 1) {
    print("Replica set is already initialized.");
  }
} catch {
  rs.initiate({
    _id: "rs0",
    members: [
      {
        _id: 0,
        host: "backup-poc-mongo:27017"
      }
    ]
  });

  print("Replica set initialization requested.");
}
