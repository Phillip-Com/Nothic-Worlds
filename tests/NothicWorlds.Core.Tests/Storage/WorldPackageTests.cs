using System.IO.Compression;
using System.Text;
using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Model;
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
    public void WrittenJson_MatchesTheGoldenVersion8File()
    {
        string path = PathFor("golden.nworld");

        WorldPackage.Save(path, RegionGoldenWorld(), AssetsWithPiece());

        Assert.Equal(Normalize(GoldenV8Json), Normalize(ReadEntry(path, "world.json")));
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

        Assert.Contains("\"formatVersion\": 8", ReadEntry(newPath, "world.json"));
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
            "future.nworld", GoldenV8Json.Replace("\"formatVersion\": 8", "\"formatVersion\": 9"),
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
        Assert.Equal(expected.Regions, actual.Regions);
        Assert.Equal(expected.Journal, actual.Journal);
        Assert.Equal(expected.Timelines, actual.Timelines);
        Assert.Equal(expected.Events, actual.Events);
        Assert.Equal(expected.Bodies.Count, actual.Bodies.Count);
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
            Assert.Equal(e.Calendar, a.Calendar);
            Assert.Equal(e.Orbit, a.Orbit);
            Assert.Equal(e.Surface.FillColor, a.Surface.FillColor);
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
