export function formatTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : "—";
}
