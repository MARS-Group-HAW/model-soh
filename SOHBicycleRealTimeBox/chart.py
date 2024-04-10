import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.dates as mdates
import itertools

from argparse import ArgumentParser

""""
    Create line chart for syncDifference of RSs from simulation run. 

    Place this script inside the results folder of the simulation run an run
    it with $ python3 ./chart.py to build chart based on BicycleRentalLayer.csv
    or specify the CSV file with chart.py -f <path>.

"""

parser = ArgumentParser()
parser.add_argument("-f", "--file", dest="file",
                    default='BicycleRentalLayer.csv',
                    help="CSV results file", metavar="FILE")
args = parser.parse_args()

# ------------------------------------------------------------------------------
# Load BicycleRentalLayer.csv into Pandas DataFrame
# ------------------------------------------------------------------------------

# name columns by hand b/c missing last Point colname in output CSV
cols = [
    'tick',
    'step',
    'dateTime',
    'type',
    'locationId',
    'name',
    'dataDescription',
    'streamId',
    'propertyDefinition',
    'thingDescription',
    'thingId',
    'time',
    'Anzahl',
    'SyncDifferenz',
    'Scenario',
    'Rents',
    'Returns',
    'Run',
    'Point'
]
df = pd.read_csv(args.file,
                 names=cols,
                 header=0,
                 parse_dates=['dateTime']
                )
stations = list(df['name'].unique())


# ------------------------------------------------------------------------------
# Build line for each rentail station
# ------------------------------------------------------------------------------
fig, ax = plt.subplots(figsize=(16, 5))
marker = itertools.cycle(('.','o','v','^','<','>','1','2','3','4','8','s','p','P','*','h','H','+','x','X','D','d','|','_'))

for name in stations:
    rs_df = df[df['name'] == name]
    ax.plot(rs_df['dateTime'], rs_df['SyncDifferenz'], label=name.replace('StadtRad-Station ', ''), marker = next(marker))

myFmt = mdates.DateFormatter('%H:%M')
ax.xaxis.set_major_formatter(myFmt)

plt.xlabel('Timeline')
plt.ylabel('Deviation per rental station')

#plt.legend(ncol=len(stations))
#plt.legend()

plt.legend(loc="lower center", bbox_to_anchor=(0.5, -1.25))
fig.subplots_adjust(bottom=0.25)

#plt.show()
plt.savefig('syncdifference_rs.png', bbox_inches='tight')
