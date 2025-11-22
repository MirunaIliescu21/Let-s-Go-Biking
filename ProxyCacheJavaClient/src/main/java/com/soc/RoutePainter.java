package com.soc;

import org.jxmapviewer.JXMapViewer;
import org.jxmapviewer.painter.Painter;
import org.jxmapviewer.viewer.GeoPosition;

import java.awt.*;
import java.awt.geom.Point2D;
import java.util.List;

/**
 * Draw a route on the map (Paris -> Lyon) + markers for origin/destination.
 */
public class RoutePainter implements Painter<JXMapViewer> {

    private final List<GeoPosition> track;

    public RoutePainter(List<GeoPosition> track) {
        this.track = track;
    }

    @Override
    public void paint(Graphics2D g, JXMapViewer map, int w, int h) {
        if (track == null || track.size() < 2) {
            return;
        }

        Graphics2D g2 = (Graphics2D) g.create();
        g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);

        Rectangle viewportBounds = map.getViewportBounds();
        g2.translate(-viewportBounds.x, -viewportBounds.y);

        g2.setColor(Color.RED);
        g2.setStroke(new BasicStroke(3f));

        Point2D prev = null;
        Point2D first = null;
        Point2D last = null;

        for (GeoPosition gp : track) {
            Point2D pt = map.getTileFactory().geoToPixel(gp, map.getZoom());

            if (first == null) {
                first = pt;
            }
            last = pt;

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

        if (first != null) {
            drawMarker(g2, first, Color.GREEN); // origin
        }
        if (last != null) {
            drawMarker(g2, last, Color.RED);   // destination
        }

        g2.dispose();
    }

    private void drawMarker(Graphics2D g2, Point2D pt, Color color) {
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
