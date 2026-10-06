using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;
using NothicWorlds.Core.Storage;

namespace NothicWorlds.Core.Tests.Storage;

public sealed class WorldPackageTests : IDisposable
{
    private const string AssetName = "assets/0123456789abcdef0123456789abcdef.png";

    // A version 1 world file, exactly as the first release wrote it. It must load forever
    // (upgraded on load). Never edit this.
    private const string GoldenV1Json = """
        {
          "formatVersion": 1,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "bodies": [
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel"
                },
                "fillColor": "#112233"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 2 world file (adds map calibration), exactly as that release wrote it. It must
    // load forever (upgraded on load). Never edit this.
    private const string GoldenV2Json = """
        {
          "formatVersion": 2,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "bodies": [
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "fillColor": "#112233"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string PieceAssetName = "assets/fedcba9876543210fedcba9876543210.png";

    // A version 3 world file (adds map pieces), exactly as this version writes it. If this test
    // fails, the file format changed: that must be deliberate, with a new format version, a
    // migration, and a new golden file. Never edit this.
    private const string GoldenV3Json = """
        {
          "formatVersion": 3,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "bodies": [
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5
                  }
                ],
                "fillColor": "#112233"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 4 world file (adds piece warps), exactly as this version writes it. If this test
    // fails, the file format changed: that must be deliberate, with a new format version, a
    // migration, and a new golden file. Never edit this.
    private const string GoldenV4Json = """
        {
          "formatVersion": 4,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "bodies": [
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 5 world file (adds star systems: body sizes, days, tilts, orbits, and the world
    // clock), exactly as this version writes it. If this test fails, the file format changed:
    // that must be deliberate, with a new format version, a migration, and a new golden file.
    // Never edit this.
    private const string GoldenV5Json = """
        {
          "formatVersion": 5,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 6 world file (adds axial tilt directions and calendars), exactly as this
    // version writes it. If this test fails, the file format changed: that must be deliberate,
    // with a new format version, a migration, and a new golden file. Never edit this.
    private const string GoldenV6Json = """
        {
          "formatVersion": 6,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                }
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 7 world file: the version 6 world with a journal, two timelines (one hidden),
    // and two events (a moment and a span) linked to the entries. Never edit this.
    private const string GoldenV7Json = """
        {
          "formatVersion": 7,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                }
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 8 world file: the version 7 world with two regions (one on the moon, one with
    // notes on the planet) and an entry placed in a region. Never edit this.
    private const string GoldenV8Json = """
        {
          "formatVersion": 8,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                }
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // A version 9 world file: the version 8 world with average temperatures and a weather
    // pin. Never edit this.
    private const string GoldenV9Json = """
        {
          "formatVersion": 9,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                }
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV10Json = """
        {
          "formatVersion": 10,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                }
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV11Json = """
        {
          "formatVersion": 11,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV12Json = """
        {
          "formatVersion": 12,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV13Json = """
        {
          "formatVersion": 13,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070"
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV14Json = """
        {
          "formatVersion": 14,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV15Json = """
        {
          "formatVersion": 15,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV16Json = """
        {
          "formatVersion": 16,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV17Json = """
        {
          "formatVersion": 17,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV18Json = """
        {
          "formatVersion": 18,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV19Json = """
        {
          "formatVersion": 19,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV20Json = """
        {
          "formatVersion": 20,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV26Json = """
        {
          "formatVersion": 26,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "style": "realistic",
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "atmosphere": true,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "atmosphere": true,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "atmosphere": false,
              "density": 2.5,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5",
                "heights": "heights/88888888888888888888888888888888.png",
                "shapes": [
                  {
                    "id": "5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a",
                    "kind": "cylinder",
                    "operation": "cut",
                    "latitude": 10,
                    "longitude": 20,
                    "depthKm": -100,
                    "widthKm": 50,
                    "heightKm": 300,
                    "lengthKm": 50
                  },
                  {
                    "id": "5b5b5b5b-5b5b-5b5b-5b5b-5b5b5b5b5b5b",
                    "kind": "box",
                    "operation": "add",
                    "latitude": -5,
                    "longitude": -30,
                    "depthKm": 2,
                    "widthKm": 40,
                    "heightKm": 4,
                    "lengthKm": 80,
                    "turn": 30
                  }
                ]
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    // The format as of version 28. If the format changes, add a version 29, a
    // migration, and a new golden file. Never edit this.
    private const string GoldenV28Json = """
        {
          "formatVersion": 28,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "style": "realistic",
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "atmosphere": true,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "atmosphere": true,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "atmosphere": false,
              "density": 2.5,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5",
                "heights": "heights/88888888888888888888888888888888.png",
                "shapes": [
                  {
                    "id": "5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a",
                    "kind": "cylinder",
                    "operation": "cut",
                    "latitude": 10,
                    "longitude": 20,
                    "depthKm": -100,
                    "widthKm": 50,
                    "heightKm": 300,
                    "lengthKm": 50
                  },
                  {
                    "id": "5b5b5b5b-5b5b-5b5b-5b5b-5b5b5b5b5b5b",
                    "kind": "box",
                    "operation": "add",
                    "latitude": -5,
                    "longitude": -30,
                    "depthKm": 2,
                    "widthKm": 40,
                    "heightKm": 4,
                    "lengthKm": 80,
                    "turn": 30
                  }
                ]
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "kind": "faction",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "kind": "place",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "relationships": [
            {
              "id": "a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1",
              "from": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "to": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "kind": "rules",
              "start": 120.5
            },
            {
              "id": "a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2",
              "from": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "to": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "kind": "other",
              "label": "pays tribute to",
              "start": 401,
              "end": 4783.25
            }
          ],
          "diagrams": [
            {
              "id": "d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1",
              "name": "The Empire and Luna",
              "entries": [
                {
                  "entry": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                  "x": 0,
                  "y": 0
                },
                {
                  "entry": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
                  "x": 240.5,
                  "y": -80
                }
              ]
            },
            {
              "id": "d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2",
              "name": "Empty"
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "starSeed": 424242,
          "constellations": [
            {
              "id": "c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1",
              "name": "The Kestrel",
              "lines": [
                [
                  22,
                  121
                ],
                [
                  121,
                  139
                ]
              ]
            },
            {
              "id": "c2c2c2c2-c2c2-c2c2-c2c2-c2c2c2c2c2c2",
              "name": "Lone Pair",
              "lines": [
                [
                  139,
                  246
                ]
              ]
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV27Json = """
        {
          "formatVersion": 27,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "style": "realistic",
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "atmosphere": true,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "atmosphere": true,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "atmosphere": false,
              "density": 2.5,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5",
                "heights": "heights/88888888888888888888888888888888.png",
                "shapes": [
                  {
                    "id": "5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a",
                    "kind": "cylinder",
                    "operation": "cut",
                    "latitude": 10,
                    "longitude": 20,
                    "depthKm": -100,
                    "widthKm": 50,
                    "heightKm": 300,
                    "lengthKm": 50
                  },
                  {
                    "id": "5b5b5b5b-5b5b-5b5b-5b5b-5b5b5b5b5b5b",
                    "kind": "box",
                    "operation": "add",
                    "latitude": -5,
                    "longitude": -30,
                    "depthKm": 2,
                    "widthKm": 40,
                    "heightKm": 4,
                    "lengthKm": 80,
                    "turn": 30
                  }
                ]
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "kind": "faction",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "kind": "place",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "relationships": [
            {
              "id": "a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1",
              "from": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "to": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "kind": "rules",
              "start": 120.5
            },
            {
              "id": "a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2",
              "from": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "to": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "kind": "other",
              "label": "pays tribute to",
              "start": 401,
              "end": 4783.25
            }
          ],
          "diagrams": [
            {
              "id": "d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1",
              "name": "The Empire and Luna",
              "entries": [
                {
                  "entry": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                  "x": 0,
                  "y": 0
                },
                {
                  "entry": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
                  "x": 240.5,
                  "y": -80
                }
              ]
            },
            {
              "id": "d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2",
              "name": "Empty"
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV25Json = """
        {
          "formatVersion": 25,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "style": "realistic",
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "density": 2.5,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5",
                "heights": "heights/88888888888888888888888888888888.png",
                "shapes": [
                  {
                    "id": "5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a",
                    "kind": "cylinder",
                    "operation": "cut",
                    "latitude": 10,
                    "longitude": 20,
                    "depthKm": -100,
                    "widthKm": 50,
                    "heightKm": 300,
                    "lengthKm": 50
                  },
                  {
                    "id": "5b5b5b5b-5b5b-5b5b-5b5b-5b5b5b5b5b5b",
                    "kind": "box",
                    "operation": "add",
                    "latitude": -5,
                    "longitude": -30,
                    "depthKm": 2,
                    "widthKm": 40,
                    "heightKm": 4,
                    "lengthKm": 80,
                    "turn": 30
                  }
                ]
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV24Json = """
        {
          "formatVersion": 24,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "density": 2.5,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5",
                "heights": "heights/88888888888888888888888888888888.png",
                "shapes": [
                  {
                    "id": "5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a",
                    "kind": "cylinder",
                    "operation": "cut",
                    "latitude": 10,
                    "longitude": 20,
                    "depthKm": -100,
                    "widthKm": 50,
                    "heightKm": 300,
                    "lengthKm": 50
                  },
                  {
                    "id": "5b5b5b5b-5b5b-5b5b-5b5b-5b5b5b5b5b5b",
                    "kind": "box",
                    "operation": "add",
                    "latitude": -5,
                    "longitude": -30,
                    "depthKm": 2,
                    "widthKm": 40,
                    "heightKm": 4,
                    "lengthKm": 80,
                    "turn": 30
                  }
                ]
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV23Json = """
        {
          "formatVersion": 23,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "density": 2.5,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5",
                "heights": "heights/88888888888888888888888888888888.png"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV22Json = """
        {
          "formatVersion": 22,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "density": 2.5,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private const string GoldenV21Json = """
        {
          "formatVersion": 21,
          "id": "11111111-2222-3333-4444-555555555555",
          "name": "Aerth",
          "createdUtc": "2026-09-30T12:00:00+00:00",
          "modifiedUtc": "2026-09-30T13:30:00+00:00",
          "timeDays": 400.5,
          "bodies": [
            {
              "id": "51515151-5151-5151-5151-515151515151",
              "name": "Sol",
              "kind": "star",
              "radiusKm": 696000,
              "dayLengthHours": 609.5,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "appearance": {
                "starType": "orange"
              },
              "belts": [
                {
                  "id": "b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1",
                  "name": "Main Belt",
                  "innerKm": 329000000,
                  "outerKm": 494000000,
                  "thickness": 12,
                  "density": 0.6,
                  "color": "#8A7F72"
                }
              ],
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aerth",
              "kind": "planet",
              "radiusKm": 6000,
              "dayLengthHours": 26.5,
              "axialTilt": 23.5,
              "axialTiltDirection": 45,
              "averageTemperature": 12.5,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 149600000,
                "periodDays": 365.25,
                "startAngle": 90
              },
              "calendar": {
                "months": [
                  {
                    "name": "Frost",
                    "days": 30
                  },
                  {
                    "name": "Highsun",
                    "days": 31
                  }
                ],
                "weekdays": [
                  "Moonday",
                  "Starday"
                ],
                "firstYear": 1203,
                "era": "of the Third Age",
                "start": {
                  "month": 1,
                  "day": 5,
                  "weekday": 1
                },
                "fit": "year-length",
                "monthMoon": "70707070-7070-7070-7070-707070707070",
                "leap": {
                  "every": 4,
                  "except": 100,
                  "exceptAgain": 400,
                  "month": 1,
                  "days": 1
                }
              },
              "appearance": {
                "color": "#336699",
                "pattern": "banded"
              },
              "rings": {
                "inner": 1.25,
                "outer": 2.3,
                "color": "#D8C8A8"
              },
              "surface": {
                "map": {
                  "asset": "assets/0123456789abcdef0123456789abcdef.png",
                  "projection": "winkel-tripel",
                  "calibration": {
                    "latitudes": [
                      {
                        "latitude": 30,
                        "drawnAs": 33.5
                      }
                    ],
                    "longitudes": [
                      {
                        "longitude": -180,
                        "drawnAs": -185
                      },
                      {
                        "longitude": 0,
                        "drawnAs": 2
                      }
                    ]
                  }
                },
                "pieces": [
                  {
                    "id": "99999999-8888-7777-6666-555555555555",
                    "name": "Northern Isles",
                    "asset": "assets/fedcba9876543210fedcba9876543210.png",
                    "outline": {
                      "sourceAspectRatio": 1.5,
                      "points": [
                        [
                          0.25,
                          0.25
                        ],
                        [
                          0.75,
                          0.25
                        ],
                        [
                          0.75,
                          0.5
                        ],
                        [
                          0.25,
                          0.5
                        ]
                      ]
                    },
                    "latitude": 55,
                    "longitude": -20.5,
                    "rotation": 15,
                    "width": 12.5,
                    "warp": [
                      [
                        0,
                        0
                      ],
                      [
                        1.25,
                        -0.125
                      ],
                      [
                        1,
                        1
                      ],
                      [
                        0,
                        1
                      ]
                    ]
                  }
                ],
                "fillColor": "#112233",
                "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
              }
            },
            {
              "id": "70707070-7070-7070-7070-707070707070",
              "name": "Luna",
              "kind": "moon",
              "shape": "flat-disc",
              "radiusKm": 1737.5,
              "dayLengthHours": 660,
              "axialTilt": 1.5,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "distanceKm": 384400,
                "periodDays": 27.5,
                "startAngle": 0,
                "eccentricity": 0.25,
                "closestApproach": 45,
                "tilt": 5.25,
                "tiltDirection": 120
              },
              "appearance": {
                "color": "#8A8A8A",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0",
              "name": "Halley",
              "kind": "comet",
              "radiusKm": 5.5,
              "dayLengthHours": 52.8,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 2667950000,
                "periodDays": 27510,
                "startAngle": 200,
                "eccentricity": 0.95,
                "closestApproach": 111,
                "tilt": 162,
                "tiltDirection": 58
              },
              "appearance": {
                "color": "#C8C4BC",
                "pattern": "rocky"
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "77777777-7777-7777-7777-777777777777",
              "name": "Yggdrasil",
              "kind": "world-tree",
              "radiusKm": 150000,
              "dayLengthHours": 8766,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "51515151-5151-5151-5151-515151515151",
                "distanceKm": 300000000,
                "periodDays": 1000,
                "startAngle": 90
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "tree": {
                "branches": 9,
                "spread": 0.9,
                "seed": 7,
                "bark": "#5A4230",
                "leaves": "#4F8A4A",
                "glow": "#FFD98A",
                "glowStrength": 1.5
              },
              "surface": {
                "fillColor": "#E6EDF5"
              }
            },
            {
              "id": "88888888-8888-8888-8888-888888888888",
              "name": "Asgard",
              "kind": "planet",
              "radiusKm": 6371,
              "dayLengthHours": 24,
              "axialTilt": 0,
              "axialTiltDirection": 0,
              "averageTemperature": 15,
              "orbit": {
                "parent": "77777777-7777-7777-7777-777777777777",
                "distanceKm": 73069.76232989965,
                "periodDays": 365.25,
                "startAngle": 92.78477261611476,
                "tiltDirection": 90,
                "height": 53888.88141821377
              },
              "appearance": {
                "color": "#214573",
                "pattern": "plain"
              },
              "branch": 0,
              "surface": {
                "fillColor": "#E6EDF5"
              }
            }
          ],
          "terrainTypes": [
            {
              "code": 1,
              "name": "Ocean",
              "color": "#1F4E79",
              "climate": "water"
            },
            {
              "code": 2,
              "name": "Shallow Water",
              "color": "#3A86B8",
              "climate": "water"
            },
            {
              "code": 3,
              "name": "Plains",
              "color": "#A8C66C",
              "climate": "open-land"
            },
            {
              "code": 4,
              "name": "Fields",
              "color": "#D8C878",
              "climate": "open-land"
            },
            {
              "code": 5,
              "name": "Forest",
              "color": "#2F6B35",
              "climate": "forest"
            },
            {
              "code": 6,
              "name": "Jungle",
              "color": "#1E5631",
              "climate": "forest"
            },
            {
              "code": 7,
              "name": "Hills",
              "color": "#8C9A5B",
              "climate": "open-land"
            },
            {
              "code": 8,
              "name": "Mountains",
              "color": "#7D6E62",
              "climate": "mountains"
            },
            {
              "code": 9,
              "name": "Desert",
              "color": "#E3C78F",
              "climate": "desert"
            },
            {
              "code": 10,
              "name": "Swamp",
              "color": "#4F6B4A",
              "climate": "wetland"
            },
            {
              "code": 11,
              "name": "Tundra",
              "color": "#A3A88E",
              "climate": "open-land"
            },
            {
              "code": 12,
              "name": "Glacier",
              "color": "#EEF3F7",
              "climate": "ice"
            },
            {
              "code": 13,
              "name": "Crystal Wastes",
              "color": "#B0E0E6",
              "climate": "desert"
            }
          ],
          "regions": [
            {
              "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "The Western Coast",
              "notes": "Fishing towns.",
              "color": "#5090D0",
              "corners": [
                [
                  10,
                  -35
                ],
                [
                  10,
                  -25
                ],
                [
                  16,
                  -25
                ],
                [
                  16.5,
                  -35
                ]
              ]
            },
            {
              "id": "4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f",
              "body": "70707070-7070-7070-7070-707070707070",
              "name": "Sea of Rain",
              "color": "#E6C878",
              "corners": [
                [
                  20,
                  10
                ],
                [
                  25,
                  30
                ],
                [
                  35,
                  15
                ]
              ]
            }
          ],
          "weatherPins": [
            {
              "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c",
              "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
              "name": "Aster Bay",
              "latitude": 42.5,
              "longitude": -71.25
            }
          ],
          "journal": [
            {
              "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
              "title": "The Founding",
              "text": "First line.\nSecond line.",
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "createdUtc": "2026-10-02T09:00:00+00:00",
              "editedUtc": "2026-10-02T10:15:00+00:00"
            },
            {
              "id": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
              "title": "Notes on Luna",
              "location": {
                "body": "70707070-7070-7070-7070-707070707070"
              },
              "createdUtc": "2026-10-02T11:00:00+00:00",
              "editedUtc": "2026-10-02T11:00:00+00:00"
            }
          ],
          "timelines": [
            {
              "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "name": "The Empire",
              "color": "#C04040"
            },
            {
              "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "name": "House Vael",
              "color": "#40A060",
              "hidden": true
            }
          ],
          "events": [
            {
              "id": "0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e",
              "timeline": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e",
              "title": "Coronation",
              "start": 120.5,
              "location": {
                "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "latitude": 12.5,
                "longitude": -30.25
              },
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"
              ]
            },
            {
              "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
              "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
              "title": "The Long War",
              "description": "Twelve years of war.",
              "start": 400,
              "end": 4783.25,
              "entries": [
                "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
                "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"
              ]
            }
          ],
          "nebulas": [
            {
              "id": "ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb",
              "name": "Veil",
              "latitude": 20,
              "longitude": 135,
              "size": 30,
              "brightness": 0.6,
              "color": "#B04080",
              "secondColor": "#4060C0"
            }
          ],
          "view": {
            "latitude": 20,
            "longitude": -45.5,
            "altitude": 1.25,
            "focusOffset": [
              0.5,
              0,
              -0.25
            ]
          }
        }
        """;

    private static readonly byte[] _imageBytes = Encoding.ASCII.GetBytes("pretend PNG bytes");
    private static readonly byte[] _pieceBytes = Encoding.ASCII.GetBytes("pretend piece PNG");

    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "nothic-worlds-tests", Guid.NewGuid().ToString("N"));

    public WorldPackageTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    // ----- Round trips -----

    [Fact]
    public void SaveThenLoad_PreservesEverything()
    {
        World original = GoldenWorld();
        string path = PathFor("aerth.nworld");

        WorldPackage.Save(path, original, AssetsFromImage());
        LoadedWorld loaded = WorldPackage.Load(path);

        AssertSameWorld(original, loaded.World);
        Assert.Equal(_imageBytes, ReadAsset(loaded, AssetName));
    }

    [Fact]
    public void SaveThenLoad_WorldWithoutMapOrView()
    {
        World original = World.CreateNew("Blank");
        string path = PathFor("blank.nworld");

        WorldPackage.Save(path, original, new Dictionary<string, IAssetSource>());
        LoadedWorld loaded = WorldPackage.Load(path);

        AssertSameWorld(original, loaded.World);
        Assert.Empty(loaded.Assets);
    }

    [Theory]
    [InlineData(MapProjection.Equirectangular)]
    [InlineData(MapProjection.Mercator)]
    [InlineData(MapProjection.Robinson)]
    [InlineData(MapProjection.WinkelTripel)]
    [InlineData(MapProjection.Mollweide)]
    [InlineData(MapProjection.GallPeters)]
    [InlineData(MapProjection.Polar)]
    [InlineData(MapProjection.TwoHemispheres)]
    public void SaveThenLoad_EveryMapType(MapProjection projection)
    {
        World original = GoldenWorld();
        original.Bodies[0].Surface.Map!.Projection = projection;
        string path = PathFor("types.nworld");

        WorldPackage.Save(path, original, AssetsFromImage());

        Assert.Equal(projection, WorldPackage.Load(path).World.Bodies[0].Surface.Map!.Projection);
    }

    [Fact]
    public void ResavingALoadedWorld_KeepsItsAssets()
    {
        string path = PathFor("resave.nworld");
        WorldPackage.Save(path, GoldenWorld(), AssetsFromImage());

        // Assets now come from the file being overwritten.
        LoadedWorld loaded = WorldPackage.Load(path);
        loaded.World.Name = "Renamed";
        WorldPackage.Save(path, loaded.World, loaded.Assets);

        LoadedWorld reloaded = WorldPackage.Load(path);
        Assert.Equal("Renamed", reloaded.World.Name);
        Assert.Equal(_imageBytes, ReadAsset(reloaded, AssetName));
    }

    // ----- The file format itself -----

    [Fact]
    public void WrittenJson_MatchesTheGoldenVersion28File()
    {
        string path = PathFor("golden.nworld");

        WorldPackage.Save(path, SkyGoldenWorld(), AssetsWithPiece());

        Assert.Equal(Normalize(GoldenV28Json), Normalize(ReadEntry(path, "world.json")));
    }

    [Fact]
    public void GoldenVersion28File_LoadsAsExpected()
    {
        AssertSameWorld(SkyGoldenWorld(), WorldPackage.Load(GoldenV28Package()).World);
    }

    [Fact]
    public void Version27Files_GetASkyFromTheirIdAndNoConstellations()
    {
        World world = WorldPackage.Load(GoldenV27Package()).World;

        Assert.Equal(StarField.SeedFor(world.Id), world.StarSeed);
        Assert.Empty(world.Constellations);
    }

    [Theory]
    [InlineData("\"starSeed\": 424242,", "")]                                 // No seed
    [InlineData("\"starSeed\": 424242", "\"starSeed\": 424243")]           // Other stars
    [InlineData("139,\n          246", "139,\n          139")]                // To itself
    [InlineData("139,\n          246", "139,\n          246,\n          22")] // Three ends
    [InlineData("121,\n          139", "121,\n          22")]                 // Drawn twice
    [InlineData("\"name\": \"Lone Pair\"", "\"name\": \"\"")]              // Unnamed
    [InlineData("c2c2c2c2-c2c2-c2c2-c2c2-c2c2c2c2c2c2",
        "c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1")]                             // One ID, twice
    public void Load_DamagedSkies_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV28Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV28Json), json);  // The edit really applied.

        string path = GoldenV28Package(json);

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    // The golden version 28 file (or a changed copy of its world.json), with its images.
    private string GoldenV28Package(string? json = null)
    {
        return WriteRawPackage("golden-v28.nworld", json ?? GoldenV28Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));
    }

    [Fact]
    public void GoldenVersion27File_LoadsAsExpected()
    {
        AssertSameWorld(DiagramGoldenWorld(), WorldPackage.Load(GoldenV27Package()).World);
    }

    [Fact]
    public void Version26Files_HaveNoKindsRelationshipsOrDiagrams()
    {
        string path = WriteRawPackage("golden-v26-upgrade.nworld", GoldenV26Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        World world = WorldPackage.Load(path).World;

        Assert.All(world.Journal, entry => Assert.Null(entry.Kind));
        Assert.Empty(world.Relationships);
        Assert.Empty(world.Diagrams);
    }

    [Theory]
    [InlineData("\"kind\": \"faction\"", "\"kind\": \"wizard\"")]       // Unknown entry kind
    [InlineData("\"kind\": \"rules\"", "\"kind\": \"haunts\"")]          // Unknown tie
    [InlineData("\"label\": \"pays tribute to\",", "")]                      // Other, no words
    [InlineData("\"start\": 401,", "\"start\": 5000,")]                       // Ends first
    [InlineData("\"to\": \"e2e2e2e2", "\"to\": \"e1e1e1e1")]                  // With itself
    [InlineData("\"from\": \"e1e1e1e1", "\"from\": \"e3e1e1e1")]              // Missing entry
    [InlineData("\"entry\": \"e2e2e2e2", "\"entry\": \"e1e1e1e1")]            // Shown twice
    [InlineData("\"entry\": \"e2e2e2e2", "\"entry\": \"e3e2e2e2")]            // Not an entry
    [InlineData("\"name\": \"Empty\"", "\"name\": \" \"")]                   // Unnamed
    [InlineData("d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2",
        "d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1")]                             // One ID, twice
    public void Load_DamagedRelationshipsAndDiagrams_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV27Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV27Json), json);  // The edit really applied.

        string path = GoldenV27Package(json);

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Theory]
    [InlineData(RelationshipKind.ParentOf, "parent-of")]
    [InlineData(RelationshipKind.MarriedTo, "married-to")]
    [InlineData(RelationshipKind.SiblingOf, "sibling-of")]
    [InlineData(RelationshipKind.AllyOf, "ally-of")]
    [InlineData(RelationshipKind.RivalOf, "rival-of")]
    [InlineData(RelationshipKind.AtWarWith, "at-war-with")]
    [InlineData(RelationshipKind.MemberOf, "member-of")]
    [InlineData(RelationshipKind.Rules, "rules")]
    [InlineData(RelationshipKind.Serves, "serves")]
    public void EachRelationshipKind_IsWrittenByNameAndReadBack(RelationshipKind kind,
        string written)
    {
        World world = DiagramGoldenWorld();
        world.Relationships[0] = world.Relationships[0] with { Kind = kind };
        string path = PathFor($"tie-{written}.nworld");

        WorldPackage.Save(path, world, AssetsWithPiece());

        Assert.Contains($"\"kind\": \"{written}\"", ReadEntry(path, "world.json"));
        Assert.Equal(kind, WorldPackage.Load(path).World.Relationships[0].Kind);
    }

    [Theory]
    [InlineData(LoreKind.Character, "character")]
    [InlineData(LoreKind.Faction, "faction")]
    [InlineData(LoreKind.Nation, "nation")]
    [InlineData(LoreKind.Place, "place")]
    [InlineData(LoreKind.Other, "other")]
    public void EachEntryKind_IsWrittenByNameAndReadBack(LoreKind kind, string written)
    {
        World world = DiagramGoldenWorld();
        world.Journal[0] = world.Journal[0] with { Kind = kind };
        string path = PathFor($"entry-{written}.nworld");

        WorldPackage.Save(path, world, AssetsWithPiece());

        Assert.Contains($"\"kind\": \"{written}\"", ReadEntry(path, "world.json"));
        Assert.Equal(kind, WorldPackage.Load(path).World.Journal[0].Kind);
    }

    // The golden version 27 file (or a changed copy of its world.json), with its images.
    private string GoldenV27Package(string? json = null)
    {
        return WriteRawPackage("golden-v27.nworld", json ?? GoldenV27Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));
    }

    [Fact]
    public void GoldenVersion26File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v26.nworld", GoldenV26Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        AssertSameWorld(AtmosphereGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version25Files_GivePlanetsAirAndMoonsNone()
    {
        string path = WriteRawPackage("golden-v25-upgrade.nworld", GoldenV25Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        World world = WorldPackage.Load(path).World;

        Assert.All(world.Bodies.Where(b => b.HasSurface),
            body => Assert.Equal(body.Kind == BodyKind.Planet, body.HasAtmosphere));
    }

    [Fact]
    public void Load_AtmosphereOnAStar_IsRefused()
    {
        string json = Normalize(GoldenV26Json).Replace("\"kind\": \"star\",",
            "\"kind\": \"star\", \"atmosphere\": true,");
        Assert.NotEqual(Normalize(GoldenV26Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-atmosphere.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion25File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v25.nworld", GoldenV25Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        AssertSameWorld(StyleGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version24Files_ArePainterly()
    {
        string path = WriteRawPackage("golden-v24-upgrade.nworld", GoldenV24Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        Assert.Equal(VisualStyle.Painterly, WorldPackage.Load(path).World.Style);
    }

    [Theory]
    [InlineData(VisualStyle.Painterly, "painterly")]
    [InlineData(VisualStyle.Realistic, "realistic")]
    [InlineData(VisualStyle.Simple, "simple")]
    public void EachStyle_IsWrittenByNameAndReadBack(VisualStyle style, string written)
    {
        World world = StyleGoldenWorld();
        world.Style = style;
        string path = PathFor($"style-{written}.nworld");

        WorldPackage.Save(path, world, AssetsWithPiece());

        Assert.Contains($"\"style\": \"{written}\"", ReadEntry(path, "world.json"));
        Assert.Equal(style, WorldPackage.Load(path).World.Style);
    }

    [Fact]
    public void Load_UnknownStyle_IsRefused()
    {
        string json = Normalize(GoldenV25Json)
            .Replace("\"style\": \"realistic\"", "\"style\": \"cubist\"");
        Assert.NotEqual(Normalize(GoldenV25Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-style.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion24File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v24.nworld", GoldenV24Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        AssertSameWorld(ShapesGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version23Files_HaveNoShapes()
    {
        string path = WriteRawPackage("golden-v23-upgrade.nworld", GoldenV23Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        Assert.All(WorldPackage.Load(path).World.Bodies,
            body => Assert.Empty(body.Surface.Shapes));
    }

    [Theory]
    [InlineData("\"kind\": \"cylinder\"", "\"kind\": \"torus\"")]
    [InlineData("\"operation\": \"cut\"", "\"operation\": \"melt\"")]
    [InlineData("\"widthKm\": 50", "\"widthKm\": 0")]
    [InlineData("\"depthKm\": -100", "\"depthKm\": -900000")]
    [InlineData("\"latitude\": 10,", "\"latitude\": 100,")]
    public void Load_InvalidShapes_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV24Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV24Json), json);  // The edit really applied.
        string path = WriteRawPackage($"bad-shape-{Math.Abs(replace.GetHashCode())}.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion23File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v23.nworld", GoldenV23Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        World world = WorldPackage.Load(path).World;

        AssertSameWorld(HeightsGoldenWorld(), world);
        HeightGrid heights = world.Bodies.Single(b => b.Name == "Asgard").Surface.Heights;
        Assert.InRange(heights.HeightAt(SphericalCoordinatesDirection(10, 20)), 2990, 3000);
        Assert.InRange(heights.HeightAt(SphericalCoordinatesDirection(-30, -50)), -1500, -1490);
    }

    [Fact]
    public void Version22Files_AreUnsculpted()
    {
        string path = WriteRawPackage("golden-v22-upgrade.nworld", GoldenV22Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.All(WorldPackage.Load(path).World.Bodies,
            body => Assert.True(body.Surface.Heights.IsEmpty));
    }

    [Theory]
    [InlineData(0)]  // None
    [InlineData(1)]  // Sub
    [InlineData(2)]  // Up
    [InlineData(3)]  // Average
    [InlineData(4)]  // Paeth
    public void Load_HeightImageSavedByAnotherTool_ReadsEveryRowFilter(byte filter)
    {
        HeightGrid heights =
            HeightsGoldenWorld().Bodies.Single(b => b.Name == "Asgard").Surface.Heights;

        string path = WriteRawPackage("tool-heights.nworld", GoldenV23Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, TestPng.Greyscale(1024, 6144, HeightPixels(heights), filter, 16)));

        Assert.True(heights.HasSameCells(WorldPackage.Load(path).World.Bodies
            .Single(b => b.Name == "Asgard").Surface.Heights));
    }

    [Fact]
    public void Load_HeightImagesThatArentRight_AreRefused()
    {
        HeightGrid heights =
            HeightsGoldenWorld().Bodies.Single(b => b.Name == "Asgard").Surface.Heights;
        byte[] belowRange = HeightPixels(heights);
        belowRange[0] = 0;  // Stored 0: a height below -32,767 m
        belowRange[1] = 0;
        var images = new (byte[] Image, string Problem)[]
        {
            (TestPng.Greyscale(1024, 6144, new byte[1024 * 6144], 0), "16-bit"),
            (TestPng.Greyscale(1024, 6144, belowRange, 0, 16), "below"),
        };

        foreach ((byte[] image, string problem) in images)
        {
            string path = WriteRawPackage($"bad-heights-{problem}.nworld", GoldenV23Json,
                (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
                (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
                (HeightsEntryName, image));

            WorldFileException error =
                Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

            Assert.Contains("height image", error.Message);
            Assert.Contains(problem, error.Message);
        }
    }

    [Fact]
    public void Load_HeightsOnAStar_AreRefused()
    {
        // Move the heights from Asgard to the sun.
        JsonObject document = JsonNode.Parse(GoldenV23Json)!.AsObject();
        JsonArray bodies = document["bodies"]!.AsArray();
        JsonNode heights = bodies.Single(b => (string?)b!["name"] == "Asgard")!["surface"]!
            .AsObject()["heights"]!;
        bodies.Single(b => (string?)b!["name"] == "Asgard")!["surface"]!.AsObject()
            .Remove("heights");
        bodies.Single(b => (string?)b!["kind"] == "star")!["surface"]!.AsObject()["heights"] =
            heights;
        string json = document.ToJsonString();
        string path = WriteRawPackage("star-heights.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())),
            (HeightsEntryName, HeightImageBytes(HeightsGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion22File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v22.nworld", GoldenV22Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(DensityGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version21Files_HaveTypicalDensities()
    {
        string path = WriteRawPackage("golden-v21-upgrade.nworld", GoldenV21Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.All(WorldPackage.Load(path).World.Bodies,
            body => Assert.Null(body.DensityGramsPerCm3));
    }

    [Theory]
    [InlineData("\"density\": 0")]
    [InlineData("\"density\": -2.5")]
    [InlineData("\"density\": 1e9")]
    public void Load_InvalidDensities_AreRefused(string replace)
    {
        string json = Normalize(GoldenV22Json).Replace("\"density\": 2.5", replace);
        Assert.NotEqual(Normalize(GoldenV22Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-density.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion21File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v21.nworld", GoldenV21Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(RealmGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version20Files_HaveNoRealms()
    {
        string path = WriteRawPackage("golden-v20-upgrade.nworld", GoldenV20Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.All(WorldPackage.Load(path).World.Bodies, body => Assert.Null(body.Branch));
    }

    [Fact]
    public void Load_ARealmsOrbit_IsSetByItsBranch()
    {
        // A hand-edited orbit is put back where the branch holds the realm.
        string json = Normalize(GoldenV21Json).Replace(
            "\"distanceKm\": 73069.76232989965", "\"distanceKm\": 99999");
        string path = WriteRawPackage("moved-realm.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        World world = WorldPackage.Load(path).World;

        Assert.Equal(73069.76232989965,
            world.Bodies.Single(b => b.Name == "Asgard").Orbit!.DistanceKm, 6);
    }

    [Theory]
    [InlineData("\"branch\": 0", "\"branch\": 30")]  // No such branch
    [InlineData("\"branch\": 0", "\"branch\": -1")]
    public void Load_InvalidRealms_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV21Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV21Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-realm.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion20File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v20.nworld", GoldenV20Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(TreeGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version19Files_HaveNoWorldTrees()
    {
        string path = WriteRawPackage("golden-v19-upgrade.nworld", GoldenV19Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.DoesNotContain(WorldPackage.Load(path).World.Bodies,
            body => body.Kind == BodyKind.WorldTree);
    }

    [Theory]
    [InlineData("\"branches\": 9", "\"branches\": 40")]          // Too many branches
    [InlineData("\"glowStrength\": 1.5", "\"glowStrength\": 9")]  // Too bright
    [InlineData("\"glow\": \"#FFD98A\"", "\"glow\": \"gold\"")]  // Not a color
    public void Load_InvalidWorldTrees_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV20Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV20Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-tree.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion19File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v19.nworld", GoldenV19Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        World world = WorldPackage.Load(path).World;

        AssertSameWorld(NebulaGoldenWorld(), world);
        Assert.Equal(NebulaGoldenWorld().Nebulas, world.Nebulas);
    }

    [Fact]
    public void Version18Files_HaveNoNebulas()
    {
        string path = WriteRawPackage("golden-v18-upgrade.nworld", GoldenV18Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.Empty(WorldPackage.Load(path).World.Nebulas);
    }

    [Theory]
    [InlineData("\"size\": 30", "\"size\": 500")]                     // Too big
    [InlineData("\"latitude\": 20,\n      \"longitude\": 135",
        "\"latitude\": 95,\n      \"longitude\": 135")]                 // Past straight up
    [InlineData("\"secondColor\": \"#4060C0\"", "\"secondColor\": \"blue\"")]
    public void Load_InvalidNebulas_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV19Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV19Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-nebula.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion18File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v18.nworld", GoldenV18Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(BeltGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version17Files_HaveNoBelts()
    {
        string path = WriteRawPackage("golden-v17-upgrade.nworld", GoldenV17Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.All(WorldPackage.Load(path).World.Bodies, body => Assert.Empty(body.Belts));
    }

    [Theory]
    [InlineData("\"outerKm\": 494000000", "\"outerKm\": 300000000")]  // Ends before it starts
    [InlineData("\"density\": 0.6", "\"density\": 2")]               // Over 100%
    [InlineData("\"name\": \"Main Belt\"", "\"name\": \" \"")]       // No name
    [InlineData("\"color\": \"#8A7F72\"", "\"color\": \"grey\"")]    // Not a color
    public void Load_InvalidBelts_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV18Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV18Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-belt.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion17File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v17.nworld", GoldenV17Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(RingsGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version16Files_HaveNoRings()
    {
        string path = WriteRawPackage("golden-v16-upgrade.nworld", GoldenV16Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.All(WorldPackage.Load(path).World.Bodies, body => Assert.Null(body.Rings));
    }

    [Theory]
    [InlineData("\"outer\": 2.3", "\"outer\": 1.1")]          // Ends before it starts
    [InlineData("\"inner\": 1.25", "\"inner\": 0.5")]         // Inside the planet
    [InlineData("\"color\": \"#D8C8A8\"", "\"color\": \"tan\"")]  // Not a color
    public void Load_InvalidRings_AreRefused(string find, string replace)
    {
        string json = Normalize(GoldenV17Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV17Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-rings.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion16File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v16.nworld", GoldenV16Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(FlatGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version15Files_HaveOnlySpheres()
    {
        string path = WriteRawPackage("golden-v15-upgrade.nworld", GoldenV15Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.All(WorldPackage.Load(path).World.Bodies,
            body => Assert.Equal(BodyShape.Sphere, body.Shape));
    }

    [Theory]
    [InlineData("\"shape\": \"flat-disc\"", "\"shape\": \"cube\"")]  // No such shape
    [InlineData("\"kind\": \"star\",", "\"kind\": \"star\",\n      \"shape\": \"flat-disc\",")]
    public void Load_InvalidShape_IsRefused(string find, string replace)
    {
        string json = Normalize(GoldenV16Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV16Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-shape.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion15File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v15.nworld", GoldenV15Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(CometGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Theory]
    // The comet circles the planet instead of a star.
    [InlineData("\"parent\": \"51515151-5151-5151-5151-515151515151\",\n" +
        "        \"distanceKm\": 2667950000",
        "\"parent\": \"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\",\n" +
        "        \"distanceKm\": 2667950000")]
    // The moon circles the comet.
    [InlineData("\"parent\": \"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\",\n" +
        "        \"distanceKm\": 384400",
        "\"parent\": \"c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0\",\n" +
        "        \"distanceKm\": 384400")]
    public void Load_BrokenCometRules_IsRefused(string find, string replace)
    {
        string json = Normalize(GoldenV15Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV15Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-comet.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion14File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v14.nworld", GoldenV14Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(LeapGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version13Files_HaveNoLeapYears()
    {
        string path = WriteRawPackage("golden-v13-upgrade.nworld", GoldenV13Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        Assert.Null(WorldPackage.Load(path).World.Bodies[1].Calendar!.Leap);
    }

    [Theory]
    [InlineData("\"except\": 100", "\"except\": 101")]     // Not a multiple of 4
    [InlineData("\"month\": 1,\n", "\"month\": 7,\n")]     // No such month
    [InlineData("\"days\": 1\n", "\"days\": 0\n")]          // Adds nothing
    public void Load_InvalidLeapRule_IsRefused(string find, string replace)
    {
        string json = Normalize(GoldenV14Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV14Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-leap.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion13File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v13.nworld", GoldenV13Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(AppearanceGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version12Files_GetEachKindsLook()
    {
        string path = WriteRawPackage("golden-v12-upgrade.nworld", GoldenV12Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        List<Body> bodies = WorldPackage.Load(path).World.Bodies;

        Assert.Equal(StarType.Yellow,
            bodies.Single(b => b.Kind == BodyKind.Star).Appearance.StarType);
        Assert.Equal(BodyAppearance.DefaultFor(BodyKind.Planet),
            bodies.Single(b => b.Kind == BodyKind.Planet).Appearance);
        Assert.Equal(BodyAppearance.DefaultFor(BodyKind.Moon),
            bodies.Single(b => b.Kind == BodyKind.Moon).Appearance);
    }

    [Theory]
    [InlineData("\"pattern\": \"banded\"", "\"pattern\": \"plaid\"")]          // Unknown pattern
    [InlineData("\"starType\": \"orange\"", "\"starType\": \"purple\"")]       // Unknown star type
    [InlineData("\"color\": \"#336699\"", "\"color\": \"sky\"")]                // Unreadable color
    public void Load_InvalidAppearance_IsRefused(string find, string replace)
    {
        string json = Normalize(GoldenV13Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV13Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-appearance.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion12File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v12.nworld", GoldenV12Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(ClimateGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version11Files_GiveDefaultNamedTypesTheirClimates_AndOthersOpenLand()
    {
        string path = WriteRawPackage("golden-v11-upgrade.nworld", GoldenV11Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        IReadOnlyList<TerrainType> types = WorldPackage.Load(path).World.TerrainTypes;

        Assert.Equal(ClimateKind.Water, types.Single(t => t.Name == "Ocean").Climate);
        Assert.Equal(ClimateKind.Wetland, types.Single(t => t.Name == "Swamp").Climate);
        Assert.Equal(ClimateKind.OpenLand, types.Single(t => t.Name == "Glacier").Climate);
        Assert.Equal(ClimateKind.OpenLand, types.Single(t => t.Name == "Crystal Wastes").Climate);
    }

    [Fact]
    public void Load_UnknownTerrainClimate_IsRefused()
    {
        string json = Normalize(GoldenV12Json)
            .Replace("\"climate\": \"desert\"", "\"climate\": \"lava\"");
        Assert.NotEqual(Normalize(GoldenV12Json), json);
        string path = WriteRawPackage("bad-climate.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("unknown terrain climate", error.Message);
    }

    [Fact]
    public void GoldenVersion11File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v11.nworld", GoldenV11Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(FittedGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version10Files_HaveNoFittedCalendars()
    {
        string path = WriteRawPackage("golden-v10-upgrade.nworld", GoldenV10Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        World world = WorldPackage.Load(path).World;

        Calendar calendar = world.Bodies[1].Calendar!;
        Assert.Equal(CalendarFit.None, calendar.Fit);
        Assert.Null(calendar.MonthMoonId);
    }

    [Theory]
    [InlineData("\"fit\": \"year-length\"", "\"fit\": \"leap-weeks\"")]        // Unknown fit
    [InlineData("\"monthMoon\": \"70707070", "\"monthMoon\": \"71707070")]      // No such body
    public void Load_InvalidCalendarFit_IsRefused(string find, string replace)
    {
        string json = Normalize(GoldenV11Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV11Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-fit.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void GoldenVersion10File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v10.nworld", GoldenV10Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        AssertSameWorld(TerrainGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version9Files_GetTheDefaultTerrainTypesAndNoPainting()
    {
        string path = WriteRawPackage("golden-v9-upgrade.nworld", GoldenV9Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        World world = WorldPackage.Load(path).World;

        Assert.Equal(TerrainType.Defaults, world.TerrainTypes);
        Assert.All(world.Bodies, body => Assert.True(body.Surface.Terrain.IsEmpty));
    }

    [Fact]
    public void UnpaintedBodies_SaveNoTerrainImage()
    {
        string path = PathFor("unpainted.nworld");

        WorldPackage.Save(path, WeatherGoldenWorld(), AssetsWithPiece());

        using ZipArchive archive = ZipFile.OpenRead(path);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.StartsWith("terrain/"));
    }

    [Fact]
    public void ATerrainImage_IsAGreyscalePngTheSizeOfTheGrid()
    {
        byte[] png = TerrainImageBytes(TerrainGoldenWorld());

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(1024, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png[16..]));
        Assert.Equal(6144, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png[20..]));
        Assert.Equal(8, png[24]);  // Bits per pixel
        Assert.Equal(0, png[25]);  // Greyscale
    }

    [Theory]
    [InlineData("\"code\": 13", "\"code\": 0")]                             // Means unpainted
    [InlineData("\"code\": 13", "\"code\": 256")]                           // Too big
    [InlineData("\"code\": 13", "\"code\": 12")]                            // Shared code
    [InlineData("\"name\": \"Crystal Wastes\"", "\"name\": \" \"")]         // Unnamed
    [InlineData("\"color\": \"#B0E0E6\"", "\"color\": \"teal\"")]           // Unreadable color
    [InlineData("\"terrain\": \"terrain/aaaa", "\"terrain\": \"../aaaa")]       // Escapes the file
    [InlineData("\"terrain\": \"terrain/aaaa", "\"terrain\": \"terrain/bbbb")]  // Missing image
    public void Load_InvalidTerrain_IsRefused(string find, string replace)
    {
        string json = Normalize(GoldenV10Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV10Json), json);  // The edit really applied.
        string path = WriteRawPackage("bad-terrain.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TerrainImageBytes(TerrainGoldenWorld())));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("is damaged", error.Message);
    }

    [Fact]
    public void Load_DamagedTerrainImage_IsRefusedWithAClearMessage()
    {
        byte[] png = TerrainImageBytes(TerrainGoldenWorld());
        png[png.Length / 2] ^= 0xFF;  // Breaks a checksum.
        string path = WriteRawPackage("damaged-terrain.nworld", GoldenV10Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes), (TerrainEntryName, png));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("terrain image", error.Message);
    }

    [Fact]
    public void Load_TerrainImageOfTheWrongSize_IsRefused()
    {
        string path = WriteRawPackage("small-terrain.nworld", GoldenV10Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TestPng.Greyscale(16, 96, new byte[16 * 96], filter: 0)));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("16 × 96", error.Message);
    }

    [Theory]
    [InlineData(0)]  // None
    [InlineData(1)]  // Sub
    [InlineData(2)]  // Up
    [InlineData(3)]  // Average
    [InlineData(4)]  // Paeth
    public void Load_TerrainImageSavedByAnotherTool_ReadsEveryRowFilter(byte filter)
    {
        // Image tools pick other row filters than this app does; all five must read back.
        World world = TerrainGoldenWorld();
        TerrainGrid grid = world.Bodies[1].Surface.Terrain;
        var cells = new byte[TerrainGrid.CellCount];
        for (int face = 0; face < 6; face++)
        {
            grid.CopyFace(face, cells.AsSpan(face * TerrainGrid.CellsPerFace));
        }

        string path = WriteRawPackage("tool-terrain.nworld", GoldenV10Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes),
            (TerrainEntryName, TestPng.Greyscale(1024, 6144, cells, filter)));

        Assert.True(grid.HasSameCells(WorldPackage.Load(path).World.Bodies[1].Surface.Terrain));
    }

    [Fact]
    public void PaintedTerrain_SurvivesARoundTrip()
    {
        World original = TerrainGoldenWorld();
        string path = PathFor("painted.nworld");

        WorldPackage.Save(path, original, AssetsWithPiece());
        TerrainGrid loaded = WorldPackage.Load(path).World.Bodies[1].Surface.Terrain;

        Assert.True(loaded.HasSameCells(original.Bodies[1].Surface.Terrain));
        Assert.Equal(5, loaded.CodeAt(SphericalCoordinatesDirection(10, -30)));
        Assert.Equal(13, loaded.CodeAt(SphericalCoordinatesDirection(-40, 100)));
        Assert.Equal(0, loaded.CodeAt(SphericalCoordinatesDirection(60, 60)));
    }

    [Fact]
    public void GoldenVersion9File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v9.nworld", GoldenV9Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        AssertSameWorld(WeatherGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version8Files_GetEarthsTemperatureAndNoWeatherPins()
    {
        string path = WriteRawPackage("golden-v8-upgrade.nworld", GoldenV8Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        World world = WorldPackage.Load(path).World;

        Assert.All(world.Bodies, body => Assert.Equal(15, body.AverageTemperatureC));
        Assert.Empty(world.WeatherPins);
    }

    [Theory]
    [InlineData("\"name\": \"Aster Bay\"", "\"name\": \" \"")]                   // Unnamed
    [InlineData("\"latitude\": 42.5", "\"latitude\": 142.5")]                     // Off the globe
    [InlineData("\"averageTemperature\": 12.5", "\"averageTemperature\": -300")]  // Too cold
    [InlineData("\"body\": \"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\",\n      \"name\": \"Aster",
        "\"body\": \"51515151-5151-5151-5151-515151515151\",\n      \"name\": \"Aster")]  // Star
    public void Load_DamagedWeather_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV9Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV9Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v9.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void GoldenVersion8File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v8.nworld", GoldenV8Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        AssertSameWorld(RegionGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version7Files_HaveNoRegions()
    {
        string path = WriteRawPackage("golden-v7-upgrade.nworld", GoldenV7Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        World world = WorldPackage.Load(path).World;

        Assert.Empty(world.Regions);
        Assert.All(world.Journal, e => Assert.Null(e.Location?.RegionId));
    }

    [Theory]
    [InlineData("\"name\": \"Sea of Rain\"", "\"name\": \"\"")]              // Unnamed
    [InlineData("\"color\": \"#5090D0\"", "\"color\": \"blue\"")]            // Unreadable color
    [InlineData("\"body\": \"70707070-7070-7070-7070-707070707070\",\n      \"name\"",
        "\"body\": \"51515151-5151-5151-5151-515151515151\",\n      \"name\"")]  // On the star
    [InlineData("\"region\": \"4e4e4e4e", "\"region\": \"4f4f4f4f")]           // Region elsewhere
    [InlineData("\"region\": \"4e4e4e4e", "\"region\": \"4d4e4e4e")]           // No such region
    [InlineData("16.5,", "95,")]                                         // Off the globe
    public void Load_DamagedRegions_AreRejected(string find, string replace)
    {
        string json = Normalize(GoldenV8Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV8Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v8.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void GoldenVersion7File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v7.nworld", GoldenV7Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        AssertSameWorld(LoreGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version6Files_HaveNoJournalOrTimelines()
    {
        string path = WriteRawPackage("golden-v6-upgrade.nworld", GoldenV6Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        World world = WorldPackage.Load(path).World;

        Assert.Empty(world.Journal);
        Assert.Empty(world.Timelines);
        Assert.Empty(world.Events);
    }

    [Theory]
    [InlineData("\"title\": \"Coronation\"", "\"title\": \"  \"")]       // Untitled event
    [InlineData("\"end\": 4783.25", "\"end\": 399")]                        // Ends before it starts
    [InlineData("\"latitude\": 12.5,", "\"latitude\": 95,")]                 // Pin off the globe
    [InlineData("\"color\": \"#C04040\"", "\"color\": \"red\"")]            // Unreadable color
    [InlineData("\"timeline\": \"7e7e7e7e", "\"timeline\": \"7d7e7e7e")]      // No such timeline
    [InlineData("\"e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2\"\n      ]",
        "\"e3e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2\"\n      ]")]                    // Missing entry
    [InlineData("\"body\": \"70707070", "\"body\": \"71707070")]             // No such body
    [InlineData("\"id\": \"e2e2e2e2", "\"id\": \"e1e1e1e1")]                 // Two entries, one ID
    public void Load_DamagedLore_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV7Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV7Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v7.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void GoldenVersion6File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v6.nworld", GoldenV6Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        AssertSameWorld(CalendarGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void Version5Files_LeanTowardZeroAndHaveNoCalendar()
    {
        string path = WriteRawPackage("golden-v5-upgrade.nworld", GoldenV5Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        World world = WorldPackage.Load(path).World;

        Assert.All(world.Bodies, body =>
        {
            Assert.Equal(0, body.AxialTiltDirectionDegrees);
            Assert.Null(body.Calendar);
        });
    }

    [Fact]
    public void GoldenVersion5File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v5.nworld", GoldenV5Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        AssertSameWorld(SystemGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void OlderFiles_GetEarthLikeBodiesAndNoOrbit()
    {
        string path = WriteRawPackage("golden-v4-upgrade.nworld", GoldenV4Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        World world = WorldPackage.Load(path).World;

        Body planet = Assert.Single(world.Bodies);
        Assert.Equal(6371, planet.RadiusKm);
        Assert.Equal(24, planet.DayLengthHours);
        Assert.Equal(0, planet.AxialTiltDegrees);
        Assert.Null(planet.Orbit);
        Assert.Equal(0, world.TimeDays);
    }

    [Fact]
    public void CircularOrbits_WriteNoExtras()
    {
        string path = PathFor("circle.nworld");

        WorldPackage.Save(path, SystemGoldenWorld(), AssetsWithPiece());

        // Only the moon's orbit has extras; the planet's circle writes just four values.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(
            ReadEntry(path, "world.json"), "\"eccentricity\""));
    }

    [Fact]
    public void GoldenVersion4File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v4.nworld", GoldenV4Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        AssertSameWorld(WarpedGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void UnwarpedPieces_WriteNoWarp()
    {
        string path = PathFor("unwarped.nworld");

        WorldPackage.Save(path, PiecesGoldenWorld(), AssetsWithPiece());

        Assert.DoesNotContain("\"warp\"", ReadEntry(path, "world.json"));
    }

    [Fact]
    public void GoldenVersion3File_LoadsAsExpected()
    {
        string path = WriteRawPackage("golden-v3.nworld", GoldenV3Json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        LoadedWorld loaded = WorldPackage.Load(path);

        AssertSameWorld(PiecesGoldenWorld(), loaded.World);
        Assert.Equal(_pieceBytes, ReadAsset(loaded, PieceAssetName));
    }

    [Fact]
    public void Save_IncludesEveryPiecesSourceImage()
    {
        string path = PathFor("pieces.nworld");

        WorldPackage.Save(path, PiecesGoldenWorld(), AssetsWithPiece());

        using ZipArchive archive = ZipFile.OpenRead(path);
        Assert.Equal(
            [AssetName, PieceAssetName, "world.json"],
            archive.Entries.Select(entry => entry.FullName).Order());
    }

    [Fact]
    public void Save_PieceImageMissing_FailsBeforeTouchingDisk()
    {
        string path = PathFor("missing-piece.nworld");

        Assert.Throws<WorldFileException>(
            () => WorldPackage.Save(path, PiecesGoldenWorld(), AssetsFromImage()));
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void GoldenVersion2File_LoadsAsExpected()
    {
        string path = WriteRawPackage(
            "golden-v2.nworld", GoldenV2Json, (AssetName, _imageBytes));

        AssertSameWorld(CalibratedGoldenWorld(), WorldPackage.Load(path).World);
    }

    [Fact]
    public void GoldenVersion1File_StillLoads_WithoutCalibration()
    {
        string path = WriteRawPackage(
            "golden-v1.nworld", GoldenV1Json, (AssetName, _imageBytes));

        World world = WorldPackage.Load(path).World;

        AssertSameWorld(GoldenWorld(), world);
        Assert.Null(world.Bodies[0].Surface.Map!.Calibration);
    }

    [Fact]
    public void ResavingAVersion1File_WritesTheCurrentVersion()
    {
        string oldPath = WriteRawPackage("old.nworld", GoldenV1Json, (AssetName, _imageBytes));
        LoadedWorld loaded = WorldPackage.Load(oldPath);
        string newPath = PathFor("upgraded.nworld");

        WorldPackage.Save(newPath, loaded.World, loaded.Assets);

        Assert.Contains("\"formatVersion\": 28", ReadEntry(newPath, "world.json"));
        AssertSameWorld(GoldenWorld(), WorldPackage.Load(newPath).World);
    }

    [Fact]
    public void Calibration_SurvivesARoundTrip()
    {
        World original = CalibratedGoldenWorld();
        MapCalibration calibration = original.Bodies[0].Surface.Map!.Calibration!;
        string path = PathFor("calibrated.nworld");

        WorldPackage.Save(path, original, AssetsFromImage());
        MapCalibration loaded =
            WorldPackage.Load(path).World.Bodies[0].Surface.Map!.Calibration!;

        foreach (double lat in new[] { -80.0, -12.0, 30.0, 47.0 })
        {
            Assert.Equal(calibration.DrawnLatitude(lat), loaded.DrawnLatitude(lat), 1e-12);
        }

        foreach (double lon in new[] { -179.0, -45.0, 0.0, 179.0 })
        {
            Assert.Equal(calibration.DrawnLongitude(lon), loaded.DrawnLongitude(lon), 1e-12);
        }
    }

    [Fact]
    public void Save_StoresTheOriginalImageUnchangedAndDropsUnusedAssets()
    {
        World world = GoldenWorld();
        var assets = new Dictionary<string, IAssetSource>(AssetsFromImage())
        {
            ["assets/ffffffffffffffffffffffffffffffff.png"] = new BytesAssetSource([1, 2, 3]),
        };
        string path = PathFor("unused.nworld");

        WorldPackage.Save(path, world, assets);

        using ZipArchive archive = ZipFile.OpenRead(path);
        Assert.Equal(
            ["assets/0123456789abcdef0123456789abcdef.png", "world.json"],
            archive.Entries.Select(entry => entry.FullName).Order());
    }

    // ----- Safe saving -----

    [Fact]
    public void SavingOverAWorld_KeepsTheOldVersionAsABackup()
    {
        string path = PathFor("backup.nworld");
        WorldPackage.Save(path, World.CreateNew("First"), new Dictionary<string, IAssetSource>());

        WorldPackage.Save(path, World.CreateNew("Second"), new Dictionary<string, IAssetSource>());

        Assert.Equal("Second", WorldPackage.Load(path).World.Name);
        Assert.Equal("First", WorldPackage.Load(path + WorldPackage.BackupSuffix).World.Name);
    }

    [Fact]
    public void FailedSave_LeavesTheExistingWorldUntouched()
    {
        string path = PathFor("safe.nworld");
        WorldPackage.Save(path, World.CreateNew("Precious"), NoAssets());
        byte[] before = File.ReadAllBytes(path);

        // The image can't be read partway through saving.
        var failing = new Dictionary<string, IAssetSource>
        {
            [AssetName] = new FailingAssetSource(),
        };
        Assert.Throws<WorldFileException>(() => WorldPackage.Save(path, GoldenWorld(), failing));

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(_folder));  // No leftover temp file.
    }

    [Fact]
    public void Save_MissingAsset_FailsBeforeTouchingDisk()
    {
        string path = PathFor("missing-asset.nworld");

        WorldFileException error = Assert.Throws<WorldFileException>(
            () => WorldPackage.Save(path, GoldenWorld(), new Dictionary<string, IAssetSource>()));

        Assert.Contains("missing", error.Message);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Save_ToAMissingFolder_ExplainsInPlainLanguage()
    {
        string path = Path.Combine(_folder, "no-such-folder", "world.nworld");

        WorldFileException error = Assert.Throws<WorldFileException>(
            () => WorldPackage.Save(path, World.CreateNew(), NoAssets()));

        Assert.Equal("Couldn't save the world: the folder doesn't exist.", error.Message);
    }

    // ----- Rejecting bad or future files -----

    [Fact]
    public void Load_NewerFormatVersion_IsRefusedWithAClearMessage()
    {
        string path = WriteRawPackage(
            "future.nworld",
            GoldenV28Json.Replace("\"formatVersion\": 28", "\"formatVersion\": 29"),
            (AssetName, _imageBytes));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("newer version", error.Message);
    }

    [Theory]
    [InlineData("\"formatVersion\": 1,", "")]                               // No version
    [InlineData("\"projection\": \"winkel-tripel\"", "\"projection\": \"cubist\"")]
    [InlineData("\"kind\": \"planet\"", "\"kind\": \"teapot\"")]
    [InlineData("\"fillColor\": \"#112233\"", "\"fillColor\": \"blue\"")]
    [InlineData("\"name\": \"Aerth\",\n  \"createdUtc\"", "\"name\": \"\",\n  \"createdUtc\"")]
    [InlineData("0123456789abcdef0123456789abcdef.png\"", "../../evil.png\"")]  // Path escape
    [InlineData("\"altitude\": 1.25", "\"altitude\": \"high\"")]
    public void Load_DamagedWorldData_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV1Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV1Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged.nworld", json, (AssetName, _imageBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Theory]
    [InlineData("\"width\": 12.5", "\"width\": 0")]             // No size
    [InlineData("\"width\": 12.5", "\"width\": 400")]           // Bigger than half the globe
    [InlineData("\"sourceAspectRatio\": 1.5", "\"sourceAspectRatio\": -1")]
    [InlineData("\"name\": \"Northern Isles\"", "\"name\": \"\"")]
    [InlineData("fedcba9876543210fedcba9876543210.png\"", "../evil.png\"")]  // Path escape
    [InlineData("0.25,", "7.5,")]  // Outline points off the image
    public void Load_DamagedPiece_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV3Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV3Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v3.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Theory]
    [InlineData("1.25,", "\"far\",")]                                    // Not a number
    [InlineData("-0.125\n", "-0.125, 3\n")]                               // Three values
    [InlineData("1.25,", "1e9,")]                                         // Absurdly far
    public void Load_DamagedWarp_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV4Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV4Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v4.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Theory]
    [InlineData("\"kind\": \"star\"", "\"kind\": \"comet\"")]                // Unknown kind
    [InlineData("\"radiusKm\": 696000", "\"radiusKm\": -1")]                  // No size
    [InlineData("\"dayLengthHours\": 26.5", "\"dayLengthHours\": 0")]         // No day
    [InlineData("\"axialTilt\": 23.5", "\"axialTilt\": 200")]                // Over 180°
    [InlineData("\"periodDays\": 365.25", "\"periodDays\": 0")]               // Never moves
    [InlineData("\"eccentricity\": 0.25", "\"eccentricity\": 1")]             // Never closes
    [InlineData("\"tilt\": 5.25", "\"tilt\": -3")]                            // Negative tilt
    [InlineData("\"timeDays\": 400.5", "\"timeDays\": \"soon\"")]              // Not a time
    [InlineData("\"parent\": \"51515151-5151-5151-5151-515151515151\"",
        "\"parent\": \"12121212-1212-1212-1212-121212121212\"")]              // Missing parent
    [InlineData("\"parent\": \"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"",
        "\"parent\": \"70707070-7070-7070-7070-707070707070\"")]              // Orbits itself
    public void Load_DamagedStarSystem_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV5Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV5Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v5.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Theory]
    [InlineData("\"days\": 30", "\"days\": 0")]                      // An empty month
    [InlineData("\"name\": \"Frost\"", "\"name\": \"\"")]             // An unnamed month
    [InlineData("\"day\": 5", "\"day\": 40")]                        // Start past the month
    [InlineData("\"weekday\": 1", "\"weekday\": 2")]                 // Start past the week
    [InlineData("\"month\": 1,", "\"month\": 7,")]                   // No such month
    [InlineData("\"axialTiltDirection\": 45", "\"axialTiltDirection\": \"east\"")]
    public void Load_DamagedCalendar_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV6Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV6Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v6.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void Load_BodiesOrbitingInALoop_IsRejected()
    {
        // The sun orbits the moon, which orbits the planet, which orbits the sun.
        string json = Normalize(GoldenV5Json).Replace(
            "\"kind\": \"star\",",
            "\"kind\": \"star\",\n\"orbit\": { \"parent\": " +
            "\"70707070-7070-7070-7070-707070707070\", \"distanceKm\": 1, " +
            "\"periodDays\": 1, \"startAngle\": 0 },");
        string path = WriteRawPackage("loop.nworld", json,
            (AssetName, _imageBytes), (PieceAssetName, _pieceBytes));

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("loop", error.Message);
    }

    [Fact]
    public void LinkToAMissingJournalEntry_IsNeverSaved()
    {
        // Like a bad warp: caught when the new file is read back, before anything is replaced.
        World world = LoreGoldenWorld();
        world.Journal.RemoveAt(1);
        string path = PathFor("dangling-link.nworld");

        WorldFileException error = Assert.Throws<WorldFileException>(
            () => WorldPackage.Save(path, world, AssetsWithPiece()));

        Assert.Contains("missing journal entry", error.Message);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void WarpWithTheWrongNumberOfPoints_IsNeverSaved()
    {
        // Saving reads the new file back before replacing anything, so a bad warp is caught
        // there and never reaches the disk.
        World world = WarpedGoldenWorld();
        world.Bodies[0].Surface.Pieces[0].WarpedPoints = [new(0, 0), new(1, 0), new(1, 1)];
        string path = PathFor("short-warp.nworld");

        WorldFileException error = Assert.Throws<WorldFileException>(
            () => WorldPackage.Save(path, world, AssetsWithPiece()));

        Assert.Contains("one finite position for each point", error.Message);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Theory]
    [InlineData("\"drawnAs\": 33.5", "\"drawnAs\": 95")]      // Latitude drawn past the pole
    [InlineData("\"drawnAs\": 2", "\"drawnAs\": 300")]        // Longitude moved half a turn
    [InlineData("\"drawnAs\": -185", "\"drawnAs\": 10")]      // Longitudes out of order
    [InlineData("\"drawnAs\": 33.5", "\"drawn\": 33.5")]      // Missing value
    public void Load_DamagedCalibration_IsRejected(string find, string replace)
    {
        string json = Normalize(GoldenV2Json).Replace(find, replace);
        Assert.NotEqual(Normalize(GoldenV2Json), json);  // The edit really applied.
        string path = WriteRawPackage("damaged-v2.nworld", json, (AssetName, _imageBytes));

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void Load_MapImageMissingFromFile_IsRejected()
    {
        string path = WriteRawPackage("no-image.nworld", GoldenV1Json);

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("missing", error.Message);
    }

    [Fact]
    public void Load_NotAZipFile_IsRejected()
    {
        string path = PathFor("text.nworld");
        File.WriteAllText(path, "just some text");

        Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));
    }

    [Fact]
    public void Load_ZipWithoutWorldData_IsRejected()
    {
        string path = PathFor("other.zip");
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            archive.CreateEntry("readme.txt");
        }

        WorldFileException error = Assert.Throws<WorldFileException>(() => WorldPackage.Load(path));

        Assert.Contains("isn't a Nothic Worlds world file", error.Message);
    }

    [Fact]
    public void Load_MissingFile_IsRejected()
    {
        Assert.Throws<WorldFileException>(() => WorldPackage.Load(PathFor("nope.nworld")));
    }

    [Fact]
    public void CreateAssetName_IsUniqueAndValid()
    {
        string first = WorldPackage.CreateAssetName(".PNG");
        string second = WorldPackage.CreateAssetName("png");

        Assert.NotEqual(first, second);
        Assert.Matches("^assets/[0-9a-f]{32}\\.png$", first);
        Assert.Throws<ArgumentException>(() => WorldPackage.CreateAssetName(".exe"));
    }

    // ----- Helpers -----

    private static World GoldenWorld()
    {
        var world = new World
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Name = "Aerth",
            CreatedUtc = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
            ModifiedUtc = new DateTimeOffset(2026, 9, 30, 13, 30, 0, TimeSpan.Zero),
            View = new CameraView(20, -45.5, 1.25, 0.5, 0, -0.25),
        };

        // What the version 28 upgrade gives a world without a sky of its own.
        world.StarSeed = StarField.SeedFor(world.Id);
        var planet = new Body
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Name = "Aerth",
            Kind = BodyKind.Planet,
        };
        planet.Surface.Map = new SurfaceMap
        {
            AssetName = AssetName,
            Projection = MapProjection.WinkelTripel,
        };
        planet.Surface.FillColor = new RgbColor(0x11, 0x22, 0x33);
        world.Bodies.Add(planet);

        // Every world has these from version 10 on, and older files are upgraded to them.
        world.TerrainTypes.AddRange(TerrainType.Defaults);
        return world;
    }

    // The golden world with the calibration in the version 2 golden file.
    private static World CalibratedGoldenWorld()
    {
        World world = GoldenWorld();
        world.Bodies[0].Surface.Map!.Calibration = MapCalibration.Create(
            [new CalibrationGuide(30, 33.5)],
            [new CalibrationGuide(-180, -185), new CalibrationGuide(0, 2)]);
        return world;
    }

    // The golden world with the calibration and the piece in the version 3 golden file.
    private static World PiecesGoldenWorld()
    {
        World world = CalibratedGoldenWorld();
        world.Bodies[0].Surface.Pieces.Add(new MapPiece
        {
            Id = Guid.Parse("99999999-8888-7777-6666-555555555555"),
            Name = "Northern Isles",
            AssetName = PieceAssetName,
            Outline = PieceOutline.Rectangle(new(0.25, 0.25), new(0.75, 0.5), 1.5),
            Center = new GeoCoordinate(55, -20.5),
            RotationDegrees = 15,
            WidthDegrees = 12.5,
        });
        return world;
    }

    private static Dictionary<string, IAssetSource> AssetsWithPiece()
    {
        return new Dictionary<string, IAssetSource>
        {
            [AssetName] = new BytesAssetSource(_imageBytes),
            [PieceAssetName] = new BytesAssetSource(_pieceBytes),
        };
    }

    // A version 9 world: the version 8 world with the planet's average temperature set to
    // 12.5 °C (the others keep the default 15) and one weather pin on it.
    // A version 28 world: the version 27 world with a sky of its own (seed 424242) and two
    // constellations, one of a single line.
    private static World SkyGoldenWorld()
    {
        World world = DiagramGoldenWorld();
        world.StarSeed = 424242;
        world.Constellations.Add(new Constellation
        {
            Id = Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1"),
            Name = "The Kestrel",
            Lines = [new(22, 121), new(121, 139)],
        });
        world.Constellations.Add(new Constellation
        {
            Id = Guid.Parse("c2c2c2c2-c2c2-c2c2-c2c2-c2c2c2c2c2c2"),
            Name = "Lone Pair",
            Lines = [new(139, 246)],
        });
        return world;
    }

    // A version 27 world: the version 26 world with its journal entries given kinds, one
    // timeless-ended tie and one dated "other" tie between them, and two diagrams, one empty.
    private static World DiagramGoldenWorld()
    {
        World world = AtmosphereGoldenWorld();
        JournalEntry founding = world.Journal[0], notes = world.Journal[1];
        world.Journal[0] = founding with { Kind = LoreKind.Faction };
        world.Journal[1] = notes with { Kind = LoreKind.Place };
        world.Relationships.Add(new Relationship
        {
            Id = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1"),
            FromEntryId = founding.Id,
            ToEntryId = notes.Id,
            Kind = RelationshipKind.Rules,
            StartDays = 120.5,
        });
        world.Relationships.Add(new Relationship
        {
            Id = Guid.Parse("a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2"),
            FromEntryId = notes.Id,
            ToEntryId = founding.Id,
            Kind = RelationshipKind.Other,
            Label = "pays tribute to",
            StartDays = 401,
            EndDays = 4783.25,
        });
        world.Diagrams.Add(new LoreDiagram
        {
            Id = Guid.Parse("d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1"),
            Name = "The Empire and Luna",
            Placements = [new(founding.Id, 0, 0), new(notes.Id, 240.5, -80)],
        });
        world.Diagrams.Add(new LoreDiagram
        {
            Id = Guid.Parse("d2d2d2d2-d2d2-d2d2-d2d2-d2d2d2d2d2d2"),
            Name = "Empty",
        });
        return world;
    }

    // A version 26 world: the version 25 world with air on its moon Luna and none on Asgard.
    private static World AtmosphereGoldenWorld()
    {
        World world = StyleGoldenWorld();
        world.Bodies.Single(b => b.Name == "Luna").HasAtmosphere = true;
        world.Bodies.Single(b => b.Name == "Asgard").HasAtmosphere = false;
        return world;
    }

    // A version 25 world: the version 24 world drawn in the Realistic style.
    private static World StyleGoldenWorld()
    {
        World world = ShapesGoldenWorld();
        world.Style = VisualStyle.Realistic;
        return world;
    }

    // A version 24 world: the version 23 world with a hole cut into Asgard and a block added.
    private static World ShapesGoldenWorld()
    {
        World world = HeightsGoldenWorld();
        world.Bodies.Single(b => b.Name == "Asgard").Surface.Shapes.AddRange(
        [
            new ShapeEdit(Guid.Parse("5a5a5a5a-5a5a-5a5a-5a5a-5a5a5a5a5a5a"), ShapeKind.Cylinder,
                ShapeOperation.Cut, new GeoCoordinate(10, 20), -100, 50, 300, 50, 0),
            new ShapeEdit(Guid.Parse("5b5b5b5b-5b5b-5b5b-5b5b-5b5b5b5b5b5b"), ShapeKind.Box,
                ShapeOperation.Add, new GeoCoordinate(-5, -30), 2, 40, 4, 80, 30),
        ]);
        return world;
    }

    // A version 23 world: the version 22 world with a hill and a basin sculpted into Asgard.
    private static World HeightsGoldenWorld()
    {
        World world = DensityGoldenWorld();
        world.Bodies.Single(b => b.Name == "Asgard").Surface.Heights = HeightGrid.Empty
            .Raise(SphericalCoordinatesDirection(10, 20), SphericalCoordinatesDirection(10, 20),
                5, 3000)
            .Raise(SphericalCoordinatesDirection(-30, -50),
                SphericalCoordinatesDirection(-30, -50), 4, -1500);
        return world;
    }

    // A version 22 world: the version 21 world with Asgard given a density.
    private static World DensityGoldenWorld()
    {
        World world = RealmGoldenWorld();
        world.Bodies.Single(b => b.Name == "Asgard").DensityGramsPerCm3 = 2.5;
        return world;
    }

    // A version 21 world: the version 20 world with Asgard hung on Yggdrasil's first branch.
    private static World RealmGoldenWorld()
    {
        World world = TreeGoldenWorld();
        Body tree = world.Bodies.Single(b => b.Kind == BodyKind.WorldTree);
        world.Bodies.Add(new Body
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            Name = "Asgard",
            Branch = 0,
            Orbit = Realms.OrbitOnBranch(tree, 0),
        });
        return world;
    }

    // A version 20 world: the version 19 world with a world tree circling the sun.
    private static World TreeGoldenWorld()
    {
        World world = NebulaGoldenWorld();
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        world.Bodies.Add(new Body
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            Name = "Yggdrasil",
            Kind = BodyKind.WorldTree,
            RadiusKm = 150_000,
            DayLengthHours = 8766,
            Tree = WorldTreeLook.Default with { Seed = 7, GlowStrength = 1.5 },
            Orbit = new Orbit
            {
                ParentId = sun.Id,
                DistanceKm = 300_000_000,
                PeriodDays = 1000,
                StartAngleDegrees = 90,
            },
        });
        return world;
    }

    // A version 19 world: the version 18 world with a nebula on the sky.
    private static World NebulaGoldenWorld()
    {
        World world = BeltGoldenWorld();
        world.Nebulas.Add(new Nebula(Guid.Parse("ebebebeb-ebeb-ebeb-ebeb-ebebebebebeb"), "Veil",
            20, 135, 30, 0.6, new RgbColor(0xB0, 0x40, 0x80), new RgbColor(0x40, 0x60, 0xC0)));
        return world;
    }

    // A version 18 world: the version 17 world with a main belt around the sun.
    private static World BeltGoldenWorld()
    {
        World world = RingsGoldenWorld();
        world.Bodies.Single(b => b.Kind == BodyKind.Star).Belts =
        [
            new AsteroidBelt(Guid.Parse("b1b1b1b1-b1b1-b1b1-b1b1-b1b1b1b1b1b1"), "Main Belt",
                329_000_000, 494_000_000, 12, 0.6, new RgbColor(0x8A, 0x7F, 0x72)),
        ];
        return world;
    }

    // A version 17 world: the version 16 world with Saturn-like rings around the planet.
    private static World RingsGoldenWorld()
    {
        World world = FlatGoldenWorld();
        world.Bodies.Single(b => b.Id == Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"))
            .Rings = PlanetRings.Default;
        return world;
    }

    // A version 16 world: the version 15 world with Luna a flat world.
    private static World FlatGoldenWorld()
    {
        World world = CometGoldenWorld();
        world.Bodies.Single(b => b.Name == "Luna").Shape = BodyShape.FlatDisc;
        return world;
    }

    // A version 15 world: the version 14 world with a Halley-like comet around the sun.
    private static World CometGoldenWorld()
    {
        World world = LeapGoldenWorld();
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        world.Bodies.Add(new Body
        {
            Id = Guid.Parse("c0c0c0c0-c0c0-c0c0-c0c0-c0c0c0c0c0c0"),
            Name = "Halley",
            Kind = BodyKind.Comet,
            Appearance = BodyAppearance.DefaultFor(BodyKind.Comet),
            RadiusKm = 5.5,
            DayLengthHours = 52.8,
            Orbit = new Orbit
            {
                ParentId = sun.Id,
                DistanceKm = 2_667_950_000,
                PeriodDays = 27_510,
                StartAngleDegrees = 200,
                Eccentricity = 0.95,
                ClosestApproachDegrees = 111,
                TiltDegrees = 162,
                TiltDirectionDegrees = 58,
            },
        });
        return world;
    }

    // A version 14 world: the version 13 world with our own leap rule on the planet's calendar
    // (every 4 years, except 100, except 400, adding a day to its second month).
    private static World LeapGoldenWorld()
    {
        World world = AppearanceGoldenWorld();
        world.Bodies[1].Calendar = world.Bodies[1].Calendar! with
        {
            Leap = new LeapRule(4, 100, 400, 1, 1),
        };
        return world;
    }

    // A version 13 world: the version 12 world with an orange sun and a blue banded planet (the
    // moon keeps its grey rocky look).
    private static World AppearanceGoldenWorld()
    {
        World world = ClimateGoldenWorld();
        Body sun = world.Bodies.Single(b => b.Kind == BodyKind.Star);
        sun.Appearance = sun.Appearance with { StarType = StarType.Orange };
        world.Bodies[1].Appearance = world.Bodies[1].Appearance with
        {
            Color = new RgbColor(0x33, 0x66, 0x99),
            Pattern = SurfacePattern.Banded,
        };
        return world;
    }

    // A version 12 world: the version 11 world with climates chosen for the renamed and the
    // added terrain types.
    private static World ClimateGoldenWorld()
    {
        World world = FittedGoldenWorld();
        world.TerrainTypes[11] = world.TerrainTypes[11] with { Climate = ClimateKind.Ice };
        world.TerrainTypes[12] = world.TerrainTypes[12] with { Climate = ClimateKind.Desert };
        return world;
    }

    // A version 11 world: the version 10 world with the planet's calendar fitting its year by
    // the year's length, and Luna as its month moon.
    private static World FittedGoldenWorld()
    {
        World world = TerrainGoldenWorld();
        world.Bodies[1].Calendar = world.Bodies[1].Calendar! with
        {
            Fit = CalendarFit.YearLength,
            MonthMoonId = Guid.Parse("70707070-7070-7070-7070-707070707070"),
        };
        return world;
    }

    // A version 10 world: the version 9 world with a renamed terrain type and a new one, and
    // terrain painted on the planet.
    private static World TerrainGoldenWorld()
    {
        World world = WeatherGoldenWorld();
        // Renamed, so a version 12 upgrade (by name) makes it open land.
        world.TerrainTypes[11] = world.TerrainTypes[11] with
        {
            Name = "Glacier",
            Climate = ClimateKind.OpenLand,
        };
        world.TerrainTypes.Add(
            new TerrainType(13, "Crystal Wastes", new RgbColor(0xB0, 0xE0, 0xE6)));
        world.Bodies[1].Surface.Terrain = TerrainGrid.Empty
            .Paint(SphericalCoordinatesDirection(10, -30), 5, 5)
            .PaintStroke(SphericalCoordinatesDirection(-40, 90),
                SphericalCoordinatesDirection(-40, 110), 3, 13);
        return world;
    }

    private const string TerrainEntryName = "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png";

    private const string HeightsEntryName = "heights/88888888888888888888888888888888.png";

    // The height image this app saves for the golden world's Asgard.
    private static byte[] HeightImageBytes(World world) => SavedEntry(world, HeightsEntryName);

    // A grid's heights as 16-bit image pixels (big-endian, 0 m stored as 32,768), the way the
    // format describes them, written independently of the app's encoder.
    private static byte[] HeightPixels(HeightGrid grid)
    {
        var cells = new short[HeightGrid.CellCount];
        for (int face = 0; face < 6; face++)
        {
            grid.CopyFace(face, cells.AsSpan(face * HeightGrid.CellsPerFace));
        }

        var pixels = new byte[cells.Length * 2];
        for (int index = 0; index < cells.Length; index++)
        {
            int stored = cells[index] + 32_768;
            pixels[index * 2] = (byte)(stored >> 8);
            pixels[index * 2 + 1] = (byte)stored;
        }

        return pixels;
    }

    // The terrain image this app saves for a world's planet.
    private static byte[] TerrainImageBytes(World world) => SavedEntry(world, TerrainEntryName);

    // An entry of the file this app saves for a world.
    private static byte[] SavedEntry(World world, string entryName)
    {
        string path = Path.Combine(
            Path.GetTempPath(), $"nothic-terrain-{Guid.NewGuid():N}.nworld");
        try
        {
            WorldPackage.Save(path, world, AssetsWithPiece());
            using ZipArchive archive = ZipFile.OpenRead(path);
            using Stream stream = archive.GetEntry(entryName)!.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + WorldPackage.BackupSuffix);
        }
    }

    private static Vector3D SphericalCoordinatesDirection(double latitude, double longitude)
    {
        System.Numerics.Vector3 direction =
            SphericalCoordinates.ToDirection(new GeoCoordinate(latitude, longitude));
        return new Vector3D(direction.X, direction.Y, direction.Z);
    }

    private static World WeatherGoldenWorld()
    {
        World world = RegionGoldenWorld();
        world.Bodies[1].AverageTemperatureC = 12.5;
        world.WeatherPins.Add(new WeatherPin
        {
            Id = Guid.Parse("3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c"),
            BodyId = world.Bodies[1].Id,
            Name = "Aster Bay",
            Spot = new GeoCoordinate(42.5, -71.25),
        });
        return world;
    }

    // A version 8 world: the version 7 world with two regions (one with notes, on the planet;
    // one without, on the moon), and the founding entry placed in the planet's region.
    private static World RegionGoldenWorld()
    {
        World world = LoreGoldenWorld();
        Guid planet = world.Bodies[1].Id;
        Guid moon = world.Bodies[2].Id;
        var coast = new Region
        {
            Id = Guid.Parse("4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e"),
            BodyId = planet,
            Name = "The Western Coast",
            Notes = "Fishing towns.",
            Color = new RgbColor(0x50, 0x90, 0xD0),
            Corners = [new(10, -35), new(10, -25), new(16, -25), new(16.5, -35)],
        };
        world.Regions.Add(coast);
        world.Regions.Add(new Region
        {
            Id = Guid.Parse("4f4f4f4f-4f4f-4f4f-4f4f-4f4f4f4f4f4f"),
            BodyId = moon,
            Name = "Sea of Rain",
            Corners = [new(20, 10), new(25, 30), new(35, 15)],
        });
        world.Journal[0] = world.Journal[0] with
        {
            Location = world.Journal[0].Location! with { RegionId = coast.Id },
        };
        return world;
    }

    // A version 7 world: the version 6 world with a journal and timelines. One entry has a
    // pinned place and two lines of text, the other just a body; one timeline is hidden; one
    // event is a moment linked to one entry, the other a span linked to both.
    private static World LoreGoldenWorld()
    {
        World world = CalendarGoldenWorld();
        Guid planet = world.Bodies[1].Id;
        Guid moon = world.Bodies[2].Id;
        var founding = new JournalEntry
        {
            Id = Guid.Parse("e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1"),
            Title = "The Founding",
            Text = "First line.\nSecond line.",
            Location = new LoreLocation(planet, new GeoCoordinate(12.5, -30.25)),
            CreatedUtc = DateTimeOffset.Parse("2026-10-02T09:00:00+00:00"),
            EditedUtc = DateTimeOffset.Parse("2026-10-02T10:15:00+00:00"),
        };
        var notes = new JournalEntry
        {
            Id = Guid.Parse("e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2"),
            Title = "Notes on Luna",
            Location = new LoreLocation(moon),
            CreatedUtc = DateTimeOffset.Parse("2026-10-02T11:00:00+00:00"),
            EditedUtc = DateTimeOffset.Parse("2026-10-02T11:00:00+00:00"),
        };
        var empire = new Timeline
        {
            Id = Guid.Parse("7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e"),
            Name = "The Empire",
            Color = new RgbColor(0xC0, 0x40, 0x40),
        };
        var vael = new Timeline
        {
            Id = Guid.Parse("7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f"),
            Name = "House Vael",
            Color = new RgbColor(0x40, 0xA0, 0x60),
            Hidden = true,
        };
        world.Journal.AddRange([founding, notes]);
        world.Timelines.AddRange([empire, vael]);
        world.Events.Add(new TimelineEvent
        {
            Id = Guid.Parse("0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e"),
            TimelineId = empire.Id,
            Title = "Coronation",
            StartDays = 120.5,
            Location = new LoreLocation(planet, new GeoCoordinate(12.5, -30.25)),
            EntryIds = [founding.Id],
        });
        world.Events.Add(new TimelineEvent
        {
            Id = Guid.Parse("0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f"),
            TimelineId = vael.Id,
            Title = "The Long War",
            Description = "Twelve years of war.",
            StartDays = 400,
            EndDays = 4783.25,
            EntryIds = [founding.Id, notes.Id],
        });
        return world;
    }

    // A version 6 world: the version 5 system, with the planet's axis leaning toward 45° and
    // the planet keeping its own calendar.
    private static World CalendarGoldenWorld()
    {
        World world = SystemGoldenWorld();
        Body planet = world.Bodies[1];
        planet.AxialTiltDirectionDegrees = 45;
        planet.Calendar = new Calendar
        {
            Months = [new("Frost", 30), new("Highsun", 31)],
            Weekdays = ["Moonday", "Starday"],
            FirstYear = 1203,
            Era = "of the Third Age",
            StartMonth = 1,
            StartDay = 5,
            StartWeekday = 1,
        };
        return world;
    }

    // A version 5 star system: a sun at the center, the mapped planet around it, and a moon on
    // an elongated, tilted orbit around the planet.
    private static World SystemGoldenWorld()
    {
        World world = WarpedGoldenWorld();
        world.TimeDays = 400.5;
        Body planet = world.Bodies[0];
        var sunId = Guid.Parse("51515151-5151-5151-5151-515151515151");
        planet.RadiusKm = 6000;
        planet.DayLengthHours = 26.5;
        planet.AxialTiltDegrees = 23.5;
        planet.Orbit = new Orbit
        {
            ParentId = sunId,
            DistanceKm = 149600000,
            PeriodDays = 365.25,
            StartAngleDegrees = 90,
        };
        world.Bodies.Insert(0, new Body
        {
            Id = sunId,
            Name = "Sol",
            Kind = BodyKind.Star,
            RadiusKm = 696000,
            DayLengthHours = 609.5,
        });
        world.Bodies.Add(new Body
        {
            Id = Guid.Parse("70707070-7070-7070-7070-707070707070"),
            Name = "Luna",
            Kind = BodyKind.Moon,
            Appearance = BodyAppearance.DefaultFor(BodyKind.Moon),
            HasAtmosphere = false,  // As files before version 26 read moons
            RadiusKm = 1737.5,
            DayLengthHours = 660,
            AxialTiltDegrees = 1.5,
            Orbit = new Orbit
            {
                ParentId = planet.Id,
                DistanceKm = 384400,
                PeriodDays = 27.5,
                StartAngleDegrees = 0,
                Eccentricity = 0.25,
                ClosestApproachDegrees = 45,
                TiltDegrees = 5.25,
                TiltDirectionDegrees = 120,
            },
        });
        return world;
    }

    private static World WarpedGoldenWorld()
    {
        World world = PiecesGoldenWorld();
        world.Bodies[0].Surface.Pieces[0].WarpedPoints =
            [new(0, 0), new(1.25, -0.125), new(1, 1), new(0, 1)];
        return world;
    }

    private static void AssertSameWorld(World expected, World actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TimeDays, actual.TimeDays);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.CreatedUtc, actual.CreatedUtc);
        Assert.Equal(expected.ModifiedUtc, actual.ModifiedUtc);
        Assert.Equal(expected.View, actual.View);
        Assert.Equal(expected.Style, actual.Style);
        Assert.Equal(expected.TerrainTypes, actual.TerrainTypes);
        Assert.Equal(expected.Regions, actual.Regions);
        Assert.Equal(expected.WeatherPins, actual.WeatherPins);
        Assert.Equal(expected.Journal, actual.Journal);
        Assert.Equal(expected.Timelines, actual.Timelines);
        Assert.Equal(expected.Events, actual.Events);
        Assert.Equal(expected.Relationships, actual.Relationships);
        Assert.Equal(expected.Diagrams, actual.Diagrams);
        Assert.Equal(expected.StarSeed, actual.StarSeed);
        Assert.Equal(expected.Constellations, actual.Constellations);
        Assert.Equal(expected.Bodies.Count, actual.Bodies.Count);
        Assert.Equal(expected.Bodies.Select(b => b.HasAtmosphere),
            actual.Bodies.Select(b => b.HasAtmosphere));
        for (int i = 0; i < expected.Bodies.Count; i++)
        {
            Body e = expected.Bodies[i];
            Body a = actual.Bodies[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.Kind, a.Kind);
            Assert.Equal(e.RadiusKm, a.RadiusKm);
            Assert.Equal(e.DayLengthHours, a.DayLengthHours);
            Assert.Equal(e.AxialTiltDegrees, a.AxialTiltDegrees);
            Assert.Equal(e.AxialTiltDirectionDegrees, a.AxialTiltDirectionDegrees);
            Assert.Equal(e.AverageTemperatureC, a.AverageTemperatureC);
            Assert.Equal(e.DensityGramsPerCm3, a.DensityGramsPerCm3);
            Assert.Equal(e.Calendar, a.Calendar);
            Assert.Equal(e.Orbit, a.Orbit);
            Assert.Equal(e.Appearance, a.Appearance);
            Assert.Equal(e.Surface.FillColor, a.Surface.FillColor);
            Assert.True(e.Surface.Terrain.HasSameCells(a.Surface.Terrain));
            Assert.True(e.Surface.Heights.HasSameCells(a.Surface.Heights));
            Assert.Equal(e.Surface.Shapes, a.Surface.Shapes);
            Assert.Equal(e.Surface.Map?.AssetName, a.Surface.Map?.AssetName);
            Assert.Equal(e.Surface.Map?.Projection, a.Surface.Map?.Projection);
            Assert.Equal(
                e.Surface.Map?.Calibration?.Latitudes, a.Surface.Map?.Calibration?.Latitudes);
            Assert.Equal(
                e.Surface.Map?.Calibration?.Longitudes, a.Surface.Map?.Calibration?.Longitudes);
            Assert.Equal(e.Surface.Pieces.Count, a.Surface.Pieces.Count);
            for (int p = 0; p < e.Surface.Pieces.Count; p++)
            {
                MapPiece ep = e.Surface.Pieces[p];
                MapPiece ap = a.Surface.Pieces[p];
                Assert.Equal(ep.Id, ap.Id);
                Assert.Equal(ep.Name, ap.Name);
                Assert.Equal(ep.AssetName, ap.AssetName);
                Assert.Equal(ep.Outline.Points, ap.Outline.Points);
                Assert.Equal(ep.Outline.SourceAspectRatio, ap.Outline.SourceAspectRatio);
                Assert.Equal(ep.Center, ap.Center);
                Assert.Equal(ep.RotationDegrees, ap.RotationDegrees);
                Assert.Equal(ep.WidthDegrees, ap.WidthDegrees);
                Assert.Equal(ep.WarpedPoints, ap.WarpedPoints);
            }
        }
    }

    private static Dictionary<string, IAssetSource> AssetsFromImage()
    {
        return new Dictionary<string, IAssetSource>
        {
            [AssetName] = new BytesAssetSource(_imageBytes),
        };
    }

    private static Dictionary<string, IAssetSource> NoAssets()
    {
        return [];
    }

    private static byte[] ReadAsset(LoadedWorld world, string name)
    {
        using Stream stream = world.Assets[name].OpenRead();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string ReadEntry(string packagePath, string entryName)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        using var reader = new StreamReader(archive.GetEntry(entryName)!.Open());
        return reader.ReadToEnd();
    }

    // Builds a world file by hand, for testing files this code didn't write.
    private string WriteRawPackage(
        string fileName, string json, params (string Name, byte[] Bytes)[] assets)
    {
        string path = PathFor(fileName);
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(archive.CreateEntry("world.json").Open()))
        {
            writer.Write(json);
        }

        foreach ((string name, byte[] bytes) in assets)
        {
            using Stream stream = archive.CreateEntry(name).Open();
            stream.Write(bytes);
        }

        return path;
    }

    private string PathFor(string fileName)
    {
        return Path.Combine(_folder, fileName);
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n").Trim();
    }

    private sealed class BytesAssetSource(byte[] bytes) : IAssetSource
    {
        public Stream OpenRead() => new MemoryStream(bytes, writable: false);
    }

    private sealed class FailingAssetSource : IAssetSource
    {
        public Stream OpenRead() => throw new IOException("Simulated disk error.");
    }
}
