% MatOct gallery - run from the repository root:  matoct docs/examples/gallery.m
% All figures use the Nature preset: Arial/Helvetica 7-8 pt at 89 mm (1 column) or 183 mm (2 columns),
% Okabe-Ito colours, ticks out, no box, exported at 300 dpi.
out = 'docs/img';
figstyle('nature');
rand('seed', 3); randn('seed', 3);
%% 0. publication-style multi-panel figure (style showcase)
f = figure('visible','off','Position',[100 100 900 300]);
t = linspace(0, 10, 200);
subplot(1,3,1);
plot(t, exp(-t/4).*sin(2*t), '-o', 'MarkerIndices', 1:12:200); hold on;
plot(t, exp(-t/4).*cos(2*t)); hold off;
xlabel('Time (s)'); ylabel('Amplitude'); legend({'sin', 'cos'}, 'Location', 'northeast');
subplot(1,3,2);
x = 1:6; y = [4.1 5.3 3.2 6.0 4.8 5.5]; e = [0.4 0.5 0.3 0.6 0.4 0.5];
bar(x, y); hold on; errorbar(x, y, e, 'k.'); hold off;
xlabel('Condition'); ylabel('Response (a.u.)');
subplot(1,3,3);
d = randn(1, 300); scatter(d, 0.6*d + 0.5*randn(1, 300), 8, 'filled');
xlabel('Predictor'); ylabel('Outcome');
panellabel;
exportgraphics(f, [out '/example-nature.png'], 'Columns', 2, 'Resolution', 300); close(f);
%% 1. line plot (single column)
f = figure('visible','off');
x = linspace(0, 2*pi, 200);
plot(x, sin(x), '-o', 'MarkerIndices', 1:20:200); hold on;
plot(x, cos(x), '--'); plot(x, sin(x).*cos(x)); hold off;
xlim([0 2*pi]); xlabel('x (rad)'); ylabel('Amplitude');
legend({'sin x', 'cos x', 'sin x cos x'}, 'Location', 'southwest');
exportgraphics(f, [out '/example-line.png'], 'Resolution', 300); close(f);
%% 2. tiledlayout
f = figure('visible','off','Position',[100 100 800 560]);
tiledlayout(2,2);
nexttile; plot(1:50, cumsum(randn(1,50))); title('Random walk'); xlabel('Step'); ylabel('Position');
nexttile; stairs(0:10, (0:10).^2/10); title('Stairs'); xlabel('n'); ylabel('n^2/10');
nexttile; stem(0:15, exp(-(0:15)/5), 'filled'); title('Stem'); xlabel('k'); ylabel('e^{-k/5}');
nexttile; scatter(randn(1,150), randn(1,150), 8, 'filled'); title('Scatter'); xlabel('x'); ylabel('y');
panellabel;
exportgraphics(f, [out '/example-tiled.png'], 'Columns', 2, 'Resolution', 300); close(f);
%% 3. surf / mesh
f = figure('visible','off','Position',[100 100 900 420]);
[X, Y] = meshgrid(linspace(-8, 8, 80));
R = sqrt(X.^2 + Y.^2) + eps; Z = sin(R)./R;
subplot(1,2,1); surf(X, Y, Z, 'EdgeColor', 'none'); colormap(parula); colorbar; view(-35, 35);
title('Ripple'); xlabel('x'); ylabel('y'); zlabel('z');
subplot(1,2,2); [X2, Y2] = meshgrid(-3:0.2:3); mesh(X2, Y2, peaks(X2, Y2)); title('peaks');
panellabel;
exportgraphics(f, [out '/example-surf.png'], 'Columns', 2, 'Resolution', 300); close(f);
%% 4. hist / bar / pie
f = figure('visible','off','Position',[100 100 1000 340]);
subplot(1,3,1); hist(randn(1,2000), 30); title('hist'); xlabel('Value'); ylabel('Count');
subplot(1,3,2); bar([3 5 2 7 4]); title('bar'); set(gca,'XTick',1:5); xlabel('Group'); ylabel('Value');
subplot(1,3,3); pie([30 20 25 25]); title('pie');
panellabel;
exportgraphics(f, [out '/example-bar-pie.png'], 'Columns', 2, 'Resolution', 300); close(f);
%% 5. imagesc
f = figure('visible','off','Position',[100 100 600 500]);
imagesc(peaks(80)); colormap(parula); colorbar; axis image;
title('imagesc(peaks(80))'); xlabel('Column'); ylabel('Row');
exportgraphics(f, [out '/example-imagesc.png'], 'Resolution', 300); close(f);
disp('gallery done');
