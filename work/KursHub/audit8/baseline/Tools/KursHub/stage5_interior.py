"""Empty room shells and navigation data in Unity-local metres.

There is no Blender dependency here. Both authoring paths consume this layout;
the accepted exterior shell, scene placement and previous prop IDs stay intact.
Reusable modules have their front toward local -Z and their origin on the floor.
"""


def layout():
    instances = []
    zones = []

    def place(name, prop, position, yaw=0):
        instances.append(dict(id=name, prop=prop, position=list(position),
                              rotation=[0, yaw, 0], scale=[1, 1, 1]))

    def room(name, use, side, floor, centre_z, front):
        """A 12 x 12 m floor plan, entered only through real front portals."""
        y = floor * 5.5
        x_front, x_back = side * 28, side * 40
        x0, x1 = sorted((x_front, x_back))
        z0, z1 = centre_z - 6, centre_z + 6
        # -Z rotated -90 faces +X on the west wing; +90 faces -X on the east.
        yaw = side * 90
        entrances = []
        if front == 'shop':
            for index, z in enumerate((centre_z - 3, centre_z + 3), 1):
                place(f'{name} Front {index:02}', 'KH_Shopfront6', (x_front, y, z), yaw)
                entrances.append([x_front, y, z])
        else:
            flank = 'KH_MeshPartition4' if front == 'residential' else 'KH_Partition4'
            for index, z in enumerate((centre_z - 4, centre_z, centre_z + 4), 1):
                prop = 'KH_DoorPartition4' if index == 2 else flank
                place(f'{name} Front {index:02}', prop, (x_front, y, z), yaw)
            entrances.append([x_front, y, centre_z])

        # Back and both sides close each floor plan. Their corners overlap by
        # half the module thickness, avoiding capsule-sized seams at joints.
        for index, z in enumerate((centre_z - 4, centre_z, centre_z + 4), 1):
            place(f'{name} Back {index:02}', 'KH_Partition4', (x_back, y, z), yaw)
        for label, z in (('South', z0), ('North', z1)):
            for index, x in enumerate((x0 + 2, x0 + 6, x0 + 10), 1):
                place(f'{name} {label} {index:02}', 'KH_Partition4', (x, y, z))

        outside, inside = side * 23, side * 33
        if front == 'shop':
            a, b = centre_z - 3, centre_z + 3
            route = [[outside, y, a], [inside, y, a], [inside, y, b], [outside, y, b]]
        else:
            route = [[outside, y, centre_z], [inside, y, centre_z],
                     [outside, y, centre_z]]
            if side == 1 and centre_z == -16:
                # The retained structural column at (24,-16) stands four
                # metres before this doorway. Use the available arcade around
                # it rather than describing a straight route through concrete.
                route = [[23,y,-18], [26,y,-18], [26,y,-16], [inside,y,-16],
                         [26,y,-16], [26,y,-18], [23,y,-18]]
        zones.append(dict(id=name, use=use, floor=floor, bounds=[x0, x1, z0, z1],
                          floorY=y, wallHeight=3.7, entrances=entrances, route=route))

    # Four metres between cells connect the public arcade to the outer service
    # walk. Nothing occupies the 30 x 50 m atrium, arrival hall or stair landings.
    for side, wing, use in ((-1, 'West', 'future trading'),
                             (1, 'East', 'future workshops')):
        for index, z in enumerate((-22, -6, 10), 1):
            room(f'Ground {wing} {index:02}', use, side, 0, z, 'shop')

    # Upper west rooms remain south of Z=16. The complete rear cross-passage
    # Z=20..31 and west roof-stair approach at X=-39.5 are kept clear.
    for index, z in enumerate((-22, -6, 10), 1):
        room(f'Upper West Residence {index:02}', 'future residential', -1, 1, z,
             'residential')
    for index, z in enumerate((-16, 0), 1):
        name = f'Upper East Medical {index:02}'
        room(name, 'future medical service', 1, 1, z, 'service')
        # Sign bottom is 2.8 m above the floor; it sits over the 2.6 m doorway.
        place(f'{name} Sign', 'KH_MedicalSign', (27.84, 8.3, z), 90)

    # The same reusable board faces an arriving player. Three metres of clear
    # height remain beneath it, including above the unchanged rear-gallery path.
    place('Ground Arrival Directions', 'KH_DirectionBoard', (0, 3.0, -38.5))
    place('Upper Rear Directions', 'KH_DirectionBoard', (0, 8.5, 25))

    routes = [
        dict(id='Ground arrival and atrium', floor=0,
             points=[[0, 0, -56], [0, 0, -44], [0, 0, -30], [10, 0, 7]]),
        dict(id='Ground west arcade', floor=0,
             points=[[-23, 0, -34], [-23, 0, -22], [-23, 0, -6], [-23, 0, 22]]),
        dict(id='Ground east arcade', floor=0,
             points=[[23, 0, -34], [23, 0, -22], [23, 0, -6], [23, 0, 22]]),
        dict(id='Upper rear and roof stair approach', floor=1,
             points=[[10, 5.5, 20], [-28, 5.5, 25], [-39.5, 5.5, 28],
                     [-39.5, 5.5, 37.5]]),
        dict(id='Upper west arcade', floor=1,
             points=[[-23, 5.5, -34], [-23, 5.5, -22], [-23, 5.5, -6],
                     [-23, 5.5, 22]]),
        dict(id='Upper east arcade', floor=1,
             points=[[23, 5.5, -34], [23, 5.5, -16], [23, 5.5, 0],
                     [23, 5.5, 22]]),
    ]

    assert len(instances) == len({p['id'] for p in instances})
    assert len(zones) == 11
    assert all(z['bounds'][3] <= 16 for z in zones)
    assert all(p['scale'] == [1, 1, 1] for p in instances)
    return dict(interiorInstances=instances, interiorZones=zones, interiorRoutes=routes)
