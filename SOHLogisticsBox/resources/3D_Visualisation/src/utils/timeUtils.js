export function formatTimestampToTime(ts) {
    const date = new Date(ts * 1000);
    return date.toISOString().substr(11, 8);
}
