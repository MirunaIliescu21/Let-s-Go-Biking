package com.soc;

import org.jxmapviewer.JXMapViewer;
import org.jxmapviewer.viewer.DefaultTileFactory;
import org.jxmapviewer.viewer.GeoPosition;
import org.jxmapviewer.viewer.TileFactoryInfo;

import javax.swing.*;
import java.awt.*;
import java.util.Arrays;
import java.util.List;

/**
 * Map + route Paris -> Lyon, with fixed zoom so that the entire line is visible.
 */
public class MapViewerApp {

    public static void main(String[] args) {

        // User-Agent required for OpenStreetMap
        System.setProperty("http.agent", "MyJavaMapClient/1.0");

        SwingUtilities.invokeLater(() -> {
            JFrame frame = new JFrame("ProxyCache SOAP client — World Map");
            frame.setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
            frame.setSize(1200, 800);

            JXMapViewer mapViewer = new JXMapViewer();

            TileFactoryInfo info = new TileFactoryInfo(
                    0, 17, 18,
                    256, true, true,
                    "https://tile.openstreetmap.org",
                    "x", "y", "z") {

                @Override
                public String getTileUrl(int x, int y, int zoom) {
                    int z = getTotalMapZoom() - zoom;
                    return String.format("%s/%d/%d/%d.png", baseURL, z, x, y);
                }
            };

            DefaultTileFactory tileFactory = new DefaultTileFactory(info);
            tileFactory.setUserAgent("MyJavaMapClient/1.0");
            mapViewer.setTileFactory(tileFactory);

            // Our route Paris -> Lyon
            GeoPosition paris = new GeoPosition(48.8566, 2.3522);
            GeoPosition lyon  = new GeoPosition(45.7640, 4.8357);

            List<GeoPosition> track = Arrays.asList(paris, lyon);

            // Route Painter + Markers
            RoutePainter routePainter = new RoutePainter(track);
            mapViewer.setOverlayPainter(routePainter);

            // Center the middle of the route
            double centerLat = (paris.getLatitude() + lyon.getLatitude()) / 2.0;
            double centerLon = (paris.getLongitude() + lyon.getLongitude()) / 2.0;
            mapViewer.setAddressLocation(new GeoPosition(centerLat, centerLon));

            // Fixed zoom
            mapViewer.setZoom(11);

            frame.setLayout(new BorderLayout());
            frame.add(mapViewer, BorderLayout.CENTER);
            frame.setLocationRelativeTo(null);
            frame.setVisible(true);
        });
    }
}
