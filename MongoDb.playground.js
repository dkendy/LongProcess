clearuse('dadosBancoCentro');

const count = db.getCollection('pessoas').countDocuments({});

const pipeline = [
  {
    $facet: {
      earliest: [
        { $sort: { receivedAt: 1 } },
        { $limit: 1 }
      ],
      latest: [
        { $sort: { receivedAt: -1 } },
        { $limit: 1 }
      ]
    }
  }
];

const results = db.getCollection('pessoas').aggregate(pipeline).toArray();
const result = results[0];

const earliest = result.earliest[0];
const latest = result.latest[0];

printjson({ Earliest: earliest, Latest: latest, Total: count });