package com.soc;

import org.jxmapviewer.JXMapViewer;
import org.jxmapviewer.painter.Painter;
import org.jxmapviewer.viewer.GeoPosition;

import java.awt.*;
import java.awt.geom.Point2D;
import java.util.List;

/**
 * Draws the route in segments:
 * - WALK: gray, dotted line
 * - BIKE: green, solid line
 * + markers for origin (green) and destination (red)
 */
public class SegmentedRoutePainter implements Painter<JXMapViewer> {

    private final List<RouteSegment> segments;

    public SegmentedRoutePainter(List<RouteSegment> segments) {
        this.segments = segments;
    }

    @Override
    public void paint(Graphics2D g, JXMapViewer map, int w, int h) {
        if (segments == null || segments.isEmpty()) {
            return;
        }

        Graphics2D g2 = (Graphics2D) g.create();
        g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);

        Rectangle viewportBounds = map.getViewportBounds();
        g2.translate(-viewportBounds.x, -viewportBounds.y);

        GeoPosition origin = null;
        GeoPosition dest = null;

        for (RouteSegment seg : segments) {
            List<GeoPosition> pts = seg.getPoints();
            if (pts.size() < 2) continue;

            // Set the style according to the mode
            if (seg.getMode() == RouteSegment.Mode.BIKE) {
                // green solid line
                g2.setColor(new Color(0, 170, 0));
                g2.setStroke(new BasicStroke(3f));
            } else {
                // WALK: gry dotted line
                float[] dash = {8f, 8f};
                g2.setColor(Color.DARK_GRAY);
                g2.setStroke(new BasicStroke(
                        3f,
                        BasicStroke.CAP_BUTT,
                        BasicStroke.JOIN_ROUND,
                        10f,
                        dash,
                        0f
                ));
            }

            Point2D prev = null;
            for (GeoPosition gp : pts) {
                Point2D pt = map.getTileFactory().geoToPixel(gp, map.getZoom());
                if (prev != null) {
                    g2.drawLine(
                            (int) prev.getX(),
                            (int) prev.getY(),
                            (int) pt.getX(),
                            (int) pt.getY()
                    );
                }
                prev = pt;
            }

            if (origin == null) {
                origin = pts.getFirst();
            }
            dest = pts.getLast();
        }

        // origin / destination markers
        if (origin != null || dest != null) {
            if (origin != null) {
                drawMarker(g2, map, origin, Color.GREEN);
            }
            if (dest != null) {
                drawMarker(g2, map, dest, Color.RED);
            }
        }

        g2.dispose();
    }

    private void drawMarker(Graphics2D g2, JXMapViewer map, GeoPosition gp, Color color) {
        Point2D pt = map.getTileFactory().geoToPixel(gp, map.getZoom());
        int size = 14;
        int x = (int) pt.getX() - size / 2;
        int y = (int) pt.getY() - size / 2;

        g2.setColor(Color.WHITE);
        g2.fillOval(x - 2, y - 2, size + 4, size + 4);

        g2.setColor(color);
        g2.fillOval(x, y, size, size);

        g2.setColor(Color.BLACK);
        g2.drawOval(x, y, size, size);
    }
}
