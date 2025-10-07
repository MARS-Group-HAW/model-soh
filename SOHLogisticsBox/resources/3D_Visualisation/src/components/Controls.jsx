export default function Controls({ isRunning, setIsRunning, speedFactor, setSpeedFactor, currentTime, formatTime }) {
    return (
        <>
            {/* Start/Stop */}
            <button
                onClick={() => setIsRunning(!isRunning)}
                style={{
                    position: 'absolute',
                    top: 20,
                    left: 20,
                    zIndex: 1,
                    padding: '10px 15px',
                    background: isRunning ? 'red' : 'green',
                    color: 'white',
                    border: 'none',
                    borderRadius: '4px',
                    cursor: 'pointer'
                }}
            >
                {isRunning ? 'Stop' : 'Start'}
            </button>

            {/* Speed */}
            <div style={{
                position: 'absolute',
                top: 70,
                left: 20,
                zIndex: 1,
                background: 'rgba(255,255,255,0)',
                padding: '10px',
                borderRadius: '4px',
                color: '#000'
            }}>
                <div>Speed: {speedFactor}x</div>
                <button onClick={() => setSpeedFactor(f => Math.max(0.1, f / 2))}>➖</button>
                <button onClick={() => setSpeedFactor(f => f * 2)}>➕</button>
            </div>

            {/* Time */}
            <div style={{
                position: 'absolute',
                top: 178,
                left: 20,
                zIndex: 1,
                background: 'rgba(255,255,255,0.6)',
                padding: '10px',
                borderRadius: '4px',
                fontFamily: 'monospace',
                color: '#000'
            }}>
                <div>Time: {currentTime ? formatTime(currentTime) : '--:--:--'}</div>
            </div>
        </>
    );
}
