# player-animation

This is a sanitized version; 
- All paid assets, and dependent code have been removed,
- Only the code supporting the movement demo video is included.
- It is as close to the version in the video otherwise.

['Point-casting'](https://github.com/ucanluc/player-animation/blob/75eec4ecbb7a6c39e46b72a3a0b4fa3b247dea7c/Assets/Scripts/Common/PointCasting/RaPointCastSnapshot.cs#L11C18-L11C37) is an example of a more straightforward implementation; as it's requirements were easier to define from the start. 

Most of the movement/animation scripts were made over time through experimentation.

Some utility functions are by the book (i.e. swing twist, PointsOnUnitSphere etc); while some are custom made (Composite force, extension functions etc).

The fun stuff starts in the [LimbedBody.cs](https://github.com/ucanluc/player-animation/blob/75eec4ecbb7a6c39e46b72a3a0b4fa3b247dea7c/Assets/Scripts/Movement/LimbedBody.cs#L292) script.
I would love to hear feedback about that thing.

- [Car model is from this free asset](https://assetstore.unity.com/packages/3d/vehicles/land/arcade-free-racing-car-161085)
- [Standart driving is modified from this free asset](https://assetstore.unity.com/packages/tools/physics/prometeo-car-controller-209444)  
