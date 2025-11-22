package com.soc;

import org.jxmapviewer.JXMapViewer;
import org.jxmapviewer.viewer.DefaultTileFactory;
import org.jxmapviewer.viewer.GeoPosition;
import org.jxmapviewer.viewer.TileFactoryInfo;

import javax.swing.*;
import java.awt.*;
import java.util.List;

/**
 * Harta + formular Origin/Destination + traseu calculat de RoutingServiceREST.
 */
public class MapViewerApp {

    public static void main(String[] args) {

        // User-Agent necesar pentru OpenStreetMap
        System.setProperty("http.agent", "MyJavaMapClient/1.0");

        SwingUtilities.invokeLater(MapViewerApp::createAndShowUI);
    }

    private static void createAndShowUI() {
        JFrame frame = new JFrame("ProxyCache SOAP client — World Map");
        frame.setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
        frame.setSize(1200, 800);

        // ====== Map viewer ======
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

        // Un centru default (Franta) pana facem primul request
        mapViewer.setAddressLocation(new GeoPosition(46.5, 2.5));
        mapViewer.setZoom(11);

        // ====== Panel de control (origin/destination) ======
        JTextField originField = new JTextField("Place du Capitole, Toulouse", 30);
        JTextField destField   = new JTextField("Gare Matabiau, Toulouse", 30);

        JButton getItineraryButton = new JButton("Get itinerary");

        JPanel controlPanel = new JPanel(new FlowLayout(FlowLayout.LEFT));
        controlPanel.add(new JLabel("Origin:"));
        controlPanel.add(originField);
        controlPanel.add(new JLabel("Destination:"));
        controlPanel.add(destField);
        controlPanel.add(getItineraryButton);

        // ====== Acțiunea butonului ======
        getItineraryButton.addActionListener(e -> {
            String origin = originField.getText().trim();
            String destination = destField.getText().trim();

            if (origin.isEmpty() || destination.isEmpty()) {
                JOptionPane.showMessageDialog(frame,
                        "Te rog să completezi Origin și Destination.",
                        "Input missing",
                        JOptionPane.WARNING_MESSAGE);
                return;
            }

            // Ca să nu blocăm UI-ul, folosim un SwingWorker (thread în fundal)
            getItineraryButton.setEnabled(false);
            getItineraryButton.setText("Loading...");

            new SwingWorker<List<GeoPosition>, Void>() {
                @Override
                protected List<GeoPosition> doInBackground() throws Exception {
                    return RoutingRestClient.fetchRoute(origin, destination);
                }

                @Override
                protected void done() {
                    try {
                        List<GeoPosition> track = get();

                        if (track.isEmpty()) {
                            JOptionPane.showMessageDialog(frame,
                                    "Nu am primit niciun punct de rută de la server.",
                                    "No route",
                                    JOptionPane.INFORMATION_MESSAGE);
                            return;
                        }

                        // Setăm painter-ul pentru noua rută
                        RoutePainter routePainter = new RoutePainter(track);
                        mapViewer.setOverlayPainter(routePainter);

                        // Centru + zoom adaptiv
                        centerAndZoom(mapViewer, track);

                        mapViewer.repaint();
                    } catch (Exception ex) {
                        ex.printStackTrace();
                        JOptionPane.showMessageDialog(frame,
                                "Eroare la obținerea itinerariului:\n" + ex.getMessage(),
                                "REST error",
                                JOptionPane.ERROR_MESSAGE);
                    } finally {
                        getItineraryButton.setEnabled(true);
                        getItineraryButton.setText("Get itinerary");
                    }
                }
            }.execute();
        });

        // ====== Layout general ======
        frame.setLayout(new BorderLayout());
        frame.add(controlPanel, BorderLayout.NORTH);
        frame.add(mapViewer, BorderLayout.CENTER);
        frame.setLocationRelativeTo(null);
        frame.setVisible(true);
    }

    /**
     * Centrează harta pe ruta dată și alege un zoom adaptiv în funcție de distanță.
     */
    private static void centerAndZoom(JXMapViewer mapViewer, List<GeoPosition> track) {
        // bounding box
        double minLat = Double.POSITIVE_INFINITY, maxLat = Double.NEGATIVE_INFINITY;
        double minLon = Double.POSITIVE_INFINITY, maxLon = Double.NEGATIVE_INFINITY;

        for (GeoPosition gp : track) {
            minLat = Math.min(minLat, gp.getLatitude());
            maxLat = Math.max(maxLat, gp.getLatitude());
            minLon = Math.min(minLon, gp.getLongitude());
            maxLon = Math.max(maxLon, gp.getLongitude());
        }

        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;
        mapViewer.setAddressLocation(new GeoPosition(centerLat, centerLon));

        // distanta aproximativa intre primul si ultimul punct (km)
        GeoPosition start = track.get(0);
        GeoPosition end = track.get(track.size() - 1);
        double distKm = haversineKm(start.getLatitude(), start.getLongitude(),
                end.getLatitude(), end.getLongitude());

        int zoom = chooseZoomFromDistanceKm(distKm);
        mapViewer.setZoom(zoom);
    }

    /**
     * Heuristica pentru zoom, bazata pe distanta aproximativa a rutei.
     * Atentie: in configuratia noastra, un numar MAI MARE inseamna zoom MAI DEPARTE
     * (din cauza inversarii pentru OpenStreetMap), deci:
     *  - trasee scurte -> numar mic
     *  - trasee lungi  -> numar mare
     */
    private static int chooseZoomFromDistanceKm(double distKm) {
        if (distKm < 3) {
            return 2;   // traseu foarte scurt in oras (ex: Capitole -> Matabiau)
        } else if (distKm < 50) {
            return 7;   // traseu mediu (prin oras / intre orase apropiate)
        } else {
            return 11;  // traseu lung (gen Paris -> Lyon)
        }
    }

    // Distanta Haversine intre doua coordonate (in km)
    private static double haversineKm(double lat1, double lon1, double lat2, double lon2) {
        double R = 6371.0; // raza Pamantului in km
        double dLat = Math.toRadians(lat2 - lat1);
        double dLon = Math.toRadians(lon2 - lon1);
        double a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                Math.cos(Math.toRadians(lat1)) * Math.cos(Math.toRadians(lat2)) *
                        Math.sin(dLon / 2) * Math.sin(dLon / 2);
        double c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
        return R * c;
    }
}
