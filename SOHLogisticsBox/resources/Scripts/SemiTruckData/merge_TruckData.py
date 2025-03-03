import pandas as pd
import numpy as np

"""
This script processes truck transport data from the German Federal Motor Transport Authority (KBA).
It combines two datasets to calculate the number of truck trips per administrative region (NUTS3) 
while categorizing by distance and weight classes.

Data sources:
- https://www.kba.de/DE/Statistik/Forschungsdatenzentrum/Datenangebot/Verkehr_europaeischer_Lastkraftfahrzeuge/verkehr_europaeischer_Lkw_node.html

Input files:
1. PUF_VD_2-V_2023.csv (Güterversand - Freight Shipping)
   - Contains transport volumes per region.
   - Three distance classes (short, regional, long-distance).

2. PUF_VD_1a_2023.csv (Lastfahrten - Loaded Trips)
   - Contains truck trip data categorized into:
     - 9 weight classes.
     - 21 distance classes with annual truck counts.
     - No information on the Region

Problem with Data:
    -Since the two files do not match but we need information from both the decision was made to merge them proportionally, where necessary.

Processing steps:
1. Load and clean both datasets.
2. Match short (<50km) and regional distance (51-150km) trips to VD_1a categories as they can be matched
3. Distribute long-distance (<150km) trips proportionally across VD_1a distance classes since we cannot match them precisely.
4. Also Weight classes cannot be matched since they only exist in VD_1a, therefore assign weight classes based on VD_1a by percentage.
5. Generate a complete dataset for all NUTS3 regions, filling missing values.
6. Remove non-German regions (codes ending in "XX000"), because for now we are only working with Germany Map.
7. Save the processed data to a CSV file (trucks_per_area with weight classes).

The final dataset provides an estimate of truck movements across Germany based on 
available statistics, allowing for further simulation and routing applications.

Possible Improvements:
    -Include Trucks with an empty Freight
    -Include Weekday statistic to get traffic not only per day but also per weekday as there is a fluctuation in weekdays
"""

# Define constants
DAYS = 1  # Change this value to set the number of days (between 1 and 365)

# Define file paths
input_file_1 = "SOHLogisticsBox/resources/Scripts/SemiTruckData/resources/puf_vd_2-V_2023.csv"
input_file_2 = "SOHLogisticsBox/resources/Scripts/SemiTruckData/resources/puf_vd_1a_2023.csv"
output_file = "SOHLogisticsBox/resources/Scripts/SemiTruckData/trucks_per_area.csv"
# Load datasets
df1 = pd.read_csv(input_file_1, delimiter=";", encoding="utf-8")
df2 = pd.read_csv(input_file_2, delimiter=";", encoding="utf-8")

# Trim column names
df1.columns = df1.columns.str.strip()
df2.columns = df2.columns.str.strip()

# Ensure required columns exist
required_columns_df1 = ["BE_NUTS3", "FT_I", "FT_ENTF3"]
required_columns_df2 = ["FT_I", "FT_ENTF21", "NULAGX"]

for col in required_columns_df1:
    if col not in df1.columns:
        raise KeyError(f"Column '{col}' not found in {input_file_1}")

for col in required_columns_df2:
    if col not in df2.columns:
        raise KeyError(f"Column '{col}' not found in {input_file_2}")

# Extract and clean relevant columns
df1_filtered = df1[["BE_NUTS3", "FT_I", "FT_ENTF3"]].copy()
df2_filtered = df2[["FT_I", "FT_ENTF21", "NULAGX"]].copy()

df1_filtered["FT_I"] = df1_filtered["FT_I"].astype(str).str.replace(',', '.').astype(float)
df2_filtered["FT_I"] = df2_filtered["FT_I"].astype(str).str.replace(',', '.').astype(float)

# Ensure FT_ENTF21 and NULAGX are numeric
df2_filtered["FT_ENTF21"] = pd.to_numeric(df2_filtered["FT_ENTF21"], errors="coerce")
df2_filtered["NULAGX"] = pd.to_numeric(df2_filtered["NULAGX"], errors="coerce")
df2_filtered = df2_filtered.dropna(subset=["FT_ENTF21", "NULAGX"])
df2_filtered["FT_ENTF21"] = df2_filtered["FT_ENTF21"].astype(int)
df2_filtered["NULAGX"] = df2_filtered["NULAGX"].astype(int)

# Step 1: Calculate the percentage distribution of FT_ENTF21 (distance classes 3-21)
df2_distribution_distance = df2_filtered[df2_filtered["FT_ENTF21"] >= 3].groupby("FT_ENTF21")["FT_I"].sum()
df2_distribution_distance_percentage = df2_distribution_distance / df2_distribution_distance.sum()

# Step 2: Calculate the percentage distribution of NULAGX (weight classes 1-9)
df2_distribution_weight = df2_filtered.groupby("NULAGX")["FT_I"].sum()
df2_distribution_weight_percentage = df2_distribution_weight / df2_distribution_weight.sum()

# Step 3: Create all possible combinations of BE_NUTS3, FT_ENTF21, and NULAGX
areas = df1_filtered["BE_NUTS3"].unique()
distance_classes = range(1, 22)  # Distance classes 1-21
weight_classes = range(1, 10)  # Weight classes 1-9

all_combinations = pd.MultiIndex.from_product([areas, distance_classes, weight_classes],
                                              names=["BE_NUTS3", "FT_ENTF21", "NULAGX"])
df_all_combinations = pd.DataFrame(index=all_combinations).reset_index()

# Step 4: Separate cases where FT_ENTF3 is 1, 2, and 3
df_class_1_2 = df1_filtered[df1_filtered["FT_ENTF3"].isin([1, 2])].copy()
df_class_3 = df1_filtered[df1_filtered["FT_ENTF3"] == 3].copy()

# Assign FT_ENTF21 based on FT_ENTF3 for classes 1 & 2
df_class_1_2["FT_ENTF21"] = df_class_1_2["FT_ENTF3"]

# Scale truck numbers correctly
df_class_1_2["FT_I"] = df_class_1_2["FT_I"] * (DAYS / 365)
df_class_1_2["FT_I"] = df_class_1_2["FT_I"].fillna(0)
df_class_1_2["FT_I"] = df_class_1_2["FT_I"].apply(lambda x: int(np.round(x / 2.0) * 2))

# Distribute truck counts for Distance Classes 1 & 2 among weight classes
df_class_1_2_expanded = []
for be_nuts3, group in df_class_1_2.groupby("BE_NUTS3"):
    for entf21 in [1, 2]:  # Distance Classes 1 and 2
        total_trucks = group[group["FT_ENTF21"] == entf21]["FT_I"].sum()  # Total trucks for this distance class

        for nulagx, weight_percentage in df2_distribution_weight_percentage.items():
            assigned_trucks = int(np.round(total_trucks * weight_percentage))  # Distribute among weight classes
            df_class_1_2_expanded.append([be_nuts3, entf21, nulagx, assigned_trucks])

# Convert to DataFrame
df_class_1_2_expanded = pd.DataFrame(df_class_1_2_expanded, columns=["BE_NUTS3", "FT_ENTF21", "NULAGX", "FT_I"])

# Distribute truck counts for Distance Classes 3-21 among weight classes
df_expanded = []
for be_nuts3, group in df_class_3.groupby("BE_NUTS3"):
    total_trucks = group["FT_I"].sum() * (DAYS / 365)
    total_trucks = max(0, int(np.round(total_trucks / 2.0) * 2))  # Ensure valid numbers

    for entf21, distance_percentage in df2_distribution_distance_percentage.items():
        trucks_for_distance = int(np.round(total_trucks * distance_percentage))

        for nulagx, weight_percentage in df2_distribution_weight_percentage.items():
            assigned_trucks = int(np.round(trucks_for_distance * weight_percentage))

            df_expanded.append([be_nuts3, entf21, nulagx, assigned_trucks])

# Convert to DataFrame
df_expanded = pd.DataFrame(df_expanded, columns=["BE_NUTS3", "FT_ENTF21", "NULAGX", "FT_I"])

# Step 5: Merge all assigned data
df_final = pd.concat([df_class_1_2_expanded, df_expanded])

# Step 6: Merge with all possible combinations to ensure complete data
df_final = df_all_combinations.merge(df_final, on=["BE_NUTS3", "FT_ENTF21", "NULAGX"], how="left")

# Step 7: Ensure FT_I is properly filled
df_final["FT_I"] = df_final["FT_I"].fillna(0).astype(int)

# Step 8: Remove non-German regions (BE_NUTS3 codes ending with "XX000")
df_final = df_final[~df_final["BE_NUTS3"].str.endswith("XX000")]

# Step 9: Save the final dataset
df_final.to_csv(output_file, sep=";", index=False, encoding="utf-8")

print(f"\nProcessed data saved to: {output_file}")