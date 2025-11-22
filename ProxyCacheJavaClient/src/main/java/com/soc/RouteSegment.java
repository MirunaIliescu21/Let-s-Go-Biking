package com.soc;

import org.jxmapviewer.viewer.GeoPosition;

import java.util.List;

public class RouteSegment {

    public enum Mode {
        WALK,
        BIKE
    }

    private final Mode mode;
    private final List<GeoPosition> points;

    public RouteSegment(Mode mode, List<GeoPosition> points) {
        this.mode = mode;
        this.points = points;
    }

    public Mode getMode() {
        return mode;
    }

    public List<GeoPosition> getPoints() {
        return points;
    }
}
