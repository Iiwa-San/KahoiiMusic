import { resolveAudio } from '../server/youtubeHelper.js';

export default async function handler(req, res) {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  const url = new URL(req.url, 'http://localhost');
  const id = url.searchParams.get('id') || req.query?.id || req.body?.id;

  if (!id) {
    return res.status(400).json({ error: 'Missing id query parameter' });
  }

  try {
    const data = await resolveAudio(id);
    return res.status(200).json(data);
  } catch (err) {
    return res.status(500).json({ error: err.message || 'Resolution failed' });
  }
}

