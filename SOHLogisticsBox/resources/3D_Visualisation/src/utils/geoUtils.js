// 🔁 Richtung in Grad
export function getHeading(from, to) {
    const [lon1, lat1] = from;
    const [lon2, lat2] = to;

    const φ1 = lat1 * Math.PI / 180;
    const φ2 = lat2 * Math.PI / 180;
    const Δλ = (lon2 - lon1) * Math.PI / 180;

    const y = Math.sin(Δλ) * Math.cos(φ2);
    const x = Math.cos(φ1) * Math.sin(φ2) -
        Math.sin(φ1) * Math.cos(φ2) * Math.cos(Δλ);

    const θ = Math.atan2(y, x);
    const bearing = (θ * 180 / Math.PI + 360) % 360;

    return -bearing + 90;
}

// 🚗 Geschwindigkeit km/h
export function getSpeed(pos1, pos2, time1, time2) {
    const [lon1, lat1] = pos1;
    const [lon2, lat2] = pos2;
    const R = 6371e3;
    const φ1 = lat1 * Math.PI / 180;
    const φ2 = lat2 * Math.PI / 180;
    const Δφ = (lat2 - lat1) * Math.PI / 180;
    const Δλ = (lon2 - lon1) * Math.PI / 180;

    const a = Math.sin(Δφ / 2) ** 2 +
        Math.cos(φ1) * Math.cos(φ2) *
        Math.sin(Δλ / 2) ** 2;

    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    const distance = R * c;
    const duration = time2 - time1;

    const speedMps = duration > 0 ? distance / duration : 0;
    return Math.round(speedMps * 3.6);
}

export function getColorFromSpeed(speed) {
    if (speed < 10) return [0, 0, 255];
    if (speed < 30) return [0, 255, 0];
    if (speed < 50) return [173, 255, 47];
    if (speed < 70) return [255, 255, 0];
    if (speed < 90) return [255, 165, 0];
    if (speed < 110) return [255, 69, 0];
    return [255, 0, 0];
}
