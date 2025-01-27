/* global use, db */
// MongoDB Playground
// To disable this template go to Settings | MongoDB | Use Default Template For Playground.
// Make sure you are connected to enable completions and to be able to run a playground.
// Use Ctrl+Space inside a snippet or a string literal to trigger completions.
// The result of the last command run in a playground is shown on the results panel.
// By default the first 20 documents will be returned with a cursor.
// Use 'console.log()' to print to the debug output.
// For more documentation on playgrounds please refer to
// https://www.mongodb.com/docs/mongodb-vscode/playgrounds/
use('dadosBancoCentro');

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
