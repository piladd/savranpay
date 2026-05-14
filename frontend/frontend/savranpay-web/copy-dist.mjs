import { cpSync, existsSync, rmSync } from 'node:fs'
import { resolve } from 'node:path'

const source = resolve('../../savranpay-web/dist')
const target = resolve('dist')

if (!existsSync(source)) {
  throw new Error(`Build output not found: ${source}`)
}

rmSync(target, { recursive: true, force: true })
cpSync(source, target, { recursive: true })
