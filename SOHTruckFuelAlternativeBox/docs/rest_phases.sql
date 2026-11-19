-- debug: select grouped rest phases (planned - start - end) per agent.
WITH phases AS (
    SELECT agentid, phaseid,
           min(tick) FILTER (WHERE eventtype = 'Planned') AS planned_tick,
           min(tick) FILTER (WHERE eventtype = 'Start') AS start_tick,
           min(tick) FILTER (WHERE eventtype = 'End') AS end_tick,
           jsonb_agg(to_jsonb(r) ORDER BY tick, id) AS events
    FROM rests r
    WHERE phaseid IS NOT NULL
    GROUP BY agentid, phaseid
)
SELECT agentid,
       jsonb_agg(jsonb_build_object(
           'phase_id', phaseid,
           'planned_tick', planned_tick,
           'start_tick', start_tick,
           'end_tick', end_tick,
           'events', events
       ) ORDER BY planned_tick, phaseid) AS phases
FROM phases
GROUP BY agentid
ORDER BY agentid;
