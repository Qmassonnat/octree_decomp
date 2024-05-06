# Octree decomposition for 3D pathfinding

This Unity project implements 3D pathfinding using octrees, and contains several 3D scenes to experiment with it.
More detailed experiments can be conducted using the [Warframe](https://movingai.com/benchmarks/warframe/index.html).

## Installation

This project requires UnityHub and Unity v2021.3.13f1
After cloning the project, open it through UnityHub and open one of the Unity scenes.
3 story building, BuildingTest, Cave, Industrial, ObstacleBuilding and Zigzag are scenes that showcase various 3D environments.
Asteroids and Young show basic scenarios of pathfinding in dynamic environments.

Each scene contains a Pathfinding GameObject, which can also be found in Assets/Prefabs, and is used to set up pathfinding.
Activate either the OctTree and AstarOctree or Voxel and AstarVoxel scripts to compute the shortest path between the Start and Target points, using octrees or the voxel baseline as the underlying data structure.
After pressing Play, the path is computed automatically.

## Setting up

Settings of the Octree script:
- All scenes are of size 20x20x20, but the octree size can be adjusted with the Bound parameter: the octree will be computed from -Bound to +Bound horizontally and 0 to 2*Bound vertically.
- MinSize defines the octree granularity or maximal level of detail, and Bound should be a power of 2 of MinSize.
- If you wish to visualize the octree, activate the Draw setting. This will noticeably reduce performance however, and this setting should be turned off for longer experiments.
- Vi and Vv should be linked to the VoxelInvalid and VoxelValid prefab, and are used for drawing the octree.
- The load setting (deactivated by default) can be used to load a previously computed octree when starting the scene. This saves time, but if you make changes to obstacles in the scene keep in mind that the previous octree may not reflect the scene accurately.

Settings of the AstarOctree script:
- By activating read_from_file, instead of computing the shortest path between Start and Target, 5,000 pairs of points will be randomly generated (this may take some time). Statistics on path length and compute times will be saved in Assets/Results.
- Activate the Draw setting to see the computed path. When path refinement is used, the original path will be displayed in red, the path obtained with the 3D funnel algorithm in yellow, and the path pruning + funnel in green. This setting should be turned off for longer experiments, are thousands of paths would need to be drawn.
- The Prune and Funnel setting toggle the use of path pruning and the funnel algorithm to do path refinement.
- In a dynamic environment, if the Move setting is activated, an agent will start moving along the path between Start and Target, and its path will be updated if the path ahead is obstructed. The setting CollDetectRange defines how far of the agent we look ahead for obstructions. If Move is not activated, we instead try maintaining a path between the start and target points.

The Voxel and AstarVoxel scripts work similarly, with the exceptions that VoxelValid should be linked to the Voxel prefab, and that some features such as path refinement and dynamic pathfinding are not implemented on this baseline. 

The DynamicObstacles script can be activated to add moving obstacles to the scene. The Obstacle setting should be linked to the Obstacle prefab, and the size, number and speed of obstacles can be adjusted with the appropriate parameters.

If you wish to change existing scenes or create your own scenes, you can use instances of the Obstacle prefab or any GameObject with the "Obstacle" tag to place your own obstacles.

## Experiments on the Warframe dataset

Finally, if you want to reproduce experiments on the Warframe dataset, open the Warframe scene.
Data from the Warframe dataset is not included in this project but can be found [here](https://movingai.com/benchmarks/warframe/index.html).
Download the maps (.3dmap files) and scenarios (.3dscen files) of the maps you want to test and place the files in Assets/Warframe.
Use the MapGenerator GameObject to select the map you want to recreate (e.g., Simple, Complex, A1...).
Results will be stored in Assets/Results/Warframe/map_name

IMPORTANT THINGS TO KNOW:
- Before starting the scene, deactivate all draw settings to speed up the process, especially the draw setting of MapGenerator which can be used to draw all obstacles of the selected Warframe map but greatly decreases performance.
- If this is not done, switching the project from debug to release mode increases performance.
- The octree MinSize should be set to 1, and the bound setting is automatically adjusted to the size of the map.
- After computing the octree on a given map for the first time, use the Load setting to very quickly load the octree during subsequent experiments.
- The read_from_file setting can be activated to compute shortest path between all 10,000 pairs of points in the corresponding scenario files. If this setting is not activated, the path between Start and Target will be computed instead. 