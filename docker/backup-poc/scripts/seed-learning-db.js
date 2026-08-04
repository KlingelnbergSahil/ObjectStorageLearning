const database = db.getSiblingDB("learning_poc");

database.samples.drop();

const now = new Date();

database.samples.insertMany([
  {
    name: "first",
    source: "seed",
    createdAt: now
  },
  {
    name: "second",
    source: "seed",
    createdAt: now
  },
  {
    name: "third",
    source: "seed",
    createdAt: now
  }
]);

printjson(database.stats());
