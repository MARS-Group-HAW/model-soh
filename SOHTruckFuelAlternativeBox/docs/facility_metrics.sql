-- Trip outcomes are emitted once, not once per tick.
SELECT outcome, count(*) AS trucks
FROM truck_trips
GROUP BY outcome
ORDER BY outcome;

SELECT count(*) AS trucks_stranded_without_energy
FROM truck_trips
WHERE outcome = 'ZeroEnergy';

-- All physical stops, including rest-only stops. Planned candidates are not counted.
SELECT agentid, count(*) AS stops
FROM rests
WHERE eventtype = 'Start'
GROUP BY agentid
ORDER BY agentid;
