[out:xml] [timeout:25];
(
  way[highway~"^(footway|pedestrian|path|steps|cycleway|living_street|sidewalk|track|residential|service|unclassified)$"]( 53.58364,9.89157,53.59532,9.93157);
  way[highway~"^(primary|secondary|tertiary|primary_link|secondary_link|tertiary_link)$"][sidewalk]( 53.58364,9.89157,53.59532,9.93157);
  way[highway~"^(primary|secondary|tertiary|primary_link|secondary_link|tertiary_link)$"][foot!=no]( 53.58364,9.89157,53.59532,9.93157);
  way[highway=path][foot!~"^no$"]( 53.58364,9.89157,53.59532,9.93157);
);
(._;>;);
out body;