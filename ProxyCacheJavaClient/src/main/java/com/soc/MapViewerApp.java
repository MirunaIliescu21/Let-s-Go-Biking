package com.soc;

import org.jxmapviewer.JXMapViewer;
import org.jxmapviewer.viewer.DefaultTileFactory;
import org.jxmapviewer.viewer.GeoPosition;
import org.jxmapviewer.viewer.TileFactoryInfo;

import javax.swing.*;
import java.awt.*;
import java.util.List;

public class MapViewerApp {

    public static void main(String[] args) {
        // User-Agent required for OpenStreetMap
        System.setProperty("http.agent", "MyJavaMapClient/1.0");
        SwingUtilities.invokeLater(MapViewerApp::createAndShowUI);
    }

    private static void createAndShowUI() {
        JFrame frame = new JFrame("ProxyCache SOAP client — World Map");
        frame.setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        frame.setSize(1200, 800);

        // Map
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

        mapViewer.setAddressLocation(new GeoPosition(46.5, 2.5));
        mapViewer.setZoom(11);

        // Controls
        JTextField originField = new JTextField("Place du Capitole, Toulouse", 30);
        JTextField destField   = new JTextField("Gare Matabiau, Toulouse", 30);
        JButton getItineraryButton = new JButton("Get itinerary");

        JPanel controlPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        controlPanel.add(new JLabel("Origin:"));
        controlPanel.add(originField);
        controlPanel.add(new JLabel("Destination:"));
        controlPanel.add(destField);
        controlPanel.add(getItineraryButton);

        getItineraryButton.addActionListener(e -> {
            String origin = originField.getText().trim();
            String destination = destField.getText().trim();

            if (origin.isEmpty() || destination.isEmpty()) {
                JOptionPane.showMessageDialog(frame,
                        "Please complete Origin and Destination.",
                        "Input missing",
                        JOptionPane.WARNING_MESSAGE);
                return;
            }

            getItineraryButton.setEnabled(false);
            getItineraryButton.setText("Loading...");

            new SwingWorker<List<RouteSegment>, Void>() {
                @Override
                protected List<RouteSegment> doInBackground() throws Exception {
                    return RoutingRestClient.fetchSegments(origin, destination);
                }

                @Override
                protected void done() {
                    try {
                        List<RouteSegment> segments = get();
                        if (segments.isEmpty()) {
                            JOptionPane.showMessageDialog(frame,
                                    "We did not receive any route segments from the server.",
                                    "No route",
                                    JOptionPane.INFORMATION_MESSAGE);
                            return;
                        }

                        // Painter with walk/bike style
                        SegmentedRoutePainter painter = new SegmentedRoutePainter(segments);
                        mapViewer.setOverlayPainter(painter);

                        centerAndZoom(mapViewer, segments);
                        mapViewer.repaint();
                    } catch (Exception ex) {
                        ex.printStackTrace();
                        JOptionPane.showMessageDialog(frame,
                                "Error getting itinerary:\n" + ex.getMessage(),
                                "REST error",
                                JOptionPane.ERROR_MESSAGE);
                    } finally {
                        getItineraryButton.setEnabled(true);
                        getItineraryButton.setText("Get itinerary");
                    }
                }
            }.execute();
        });

        frame.setLayout(new BorderLayout());
        frame.add(controlPanel, BorderLayout.NORTH);
        frame.add(mapViewer, BorderLayout.CENTER);
        frame.setLocationRelativeTo(null);
        frame.setVisible(true);
    }

    private static void centerAndZoom(JXMapViewer mapViewer, List<RouteSegment> segments) {
        double minLat = Double.POSITIVE_INFINITY, maxLat = Double.NEGATIVE_INFINITY;
        double minLon = Double.POSITIVE_INFINITY, maxLon = Double.NEGATIVE_INFINITY;

        GeoPosition start = null;
        GeoPosition end = null;

        for (RouteSegment seg : segments) {
            for (GeoPosition gp : seg.getPoints()) {
                minLat = Math.min(minLat, gp.getLatitude());
                maxLat = Math.max(maxLat, gp.getLatitude());
                minLon = Math.min(minLon, gp.getLongitude());
                maxLon = Math.max(maxLon, gp.getLongitude());
            }
            if (start == null && !seg.getPoints().isEmpty()) {
                start = seg.getPoints().getFirst();
            }
            if (!seg.getPoints().isEmpty()) {
                end = seg.getPoints().getLast();
            }
        }

        if (start == null || end == null) {
            return;
        }

        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;
        mapViewer.setAddressLocation(new GeoPosition(centerLat, centerLon));

        double distKm = haversineKm(start.getLatitude(), start.getLongitude(),
                end.getLatitude(), end.getLongitude());

        int zoom = chooseZoomFromDistanceKm(distKm);
        mapViewer.setZoom(zoom);
    }

    private static int chooseZoomFromDistanceKm(double distKm) {
        if (distKm < 3) {
            return 2;
        } else if (distKm < 50) {
            return 7;
        } else {
            return 11;
        }
    }

    private static double haversineKm(double lat1, double lon1, double lat2, double lon2) {
        double R = 6371.0;
        double dLat = Math.toRadians(lat2 - lat1);
        double dLon = Math.toRadians(lon2 - lon1);
        double a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                Math.cos(Math.toRadians(lat1)) * Math.cos(Math.toRadians(lat2)) *
                        Math.sin(dLon / 2) * Math.sin(dLon / 2);
        double c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
        return R * c;
    }
}
