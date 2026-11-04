const { searchYouTube } = require('./youtubeHelper');

module.exports = async function handler(req, res) {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  const url = new URL(req.url, 'http://localhost');
  const q = url.searchParams.get('q') || req.query?.q || req.body?.q || '';

  try {
    const data = await searchYouTube(q);
    return res.status(200).json(data);
  } catch (err) {
    return res.status(500).json({ error: err.message || 'Search failed' });
  }
};

