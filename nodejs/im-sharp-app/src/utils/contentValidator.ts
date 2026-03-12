const SCRIPT_PATTERNS = [
  /<\s*script[\s\S]*?>/i,
  /<\s*\/\s*script\s*>/i,
  /javascript\s*:/i,
  /vbscript\s*:/i,
  /\bon\w+\s*=/i,
  /data\s*:\s*text\/html/i,
  /expression\s*\(/i,
]

export function containsScript(content: string): boolean {
  return SCRIPT_PATTERNS.some(p => p.test(content))
}
