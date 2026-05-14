const handler = require('./[...path].js')

module.exports = async function router(req, res) {
  const marker = '/api/v1/'
  const path = req.url.includes(marker) ? req.url.split(marker)[1].split('?')[0] : ''
  req.query = { ...req.query, path: path ? path.split('/') : [] }
  return handler(req, res)
}
