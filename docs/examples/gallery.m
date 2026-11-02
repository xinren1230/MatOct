% MatOct gallery - run from the repository root:  matrw docs/examples/gallery.m
out = 'docs/img';
%% 1. styled 2-D line plot
f = figure('visible','off','Position',[100 100 800 500]);
x = linspace(0, 2*pi, 40);
plot(x, sin(x), '-o', 'LineWidth', 2, 'MarkerFaceColor', [0.85 0.33 0.1], 'MarkerSize', 6); hold on;
plot(x, cos(x), '--s', 'LineWidth', 1.5, 'Color', [0.64 0.08 0.18]);
plot(x, sin(x).*cos(x), ':', 'LineWidth', 2.5, 'Color', [0.47 0.67 0.19]); hold off;
grid on; xlim([0 2*pi]);
title('Styled lines with markers', 'FontSize', 14); xlabel('x (rad)'); ylabel('amplitude');
legend({'sin x', 'cos x', 'sin x cos x'}, 'Location', 'southwest');
set(gca, 'FontSize', 11, 'TickDir', 'out');
exportgraphics(f, [out '/example-line.png'], 'Resolution', 110); close(f);
%% 2. tiledlayout / subplot
f = figure('visible','off','Position',[100 100 900 600]);
tiledlayout(2,2);
nexttile; plot(1:20, cumsum(randn(1,20)), 'LineWidth', 2); title('random walk'); grid on;
nexttile; stairs(0:10, (0:10).^2/10, 'LineWidth', 1.5); title('stairs');
nexttile; stem(0:15, exp(-(0:15)/5), 'filled'); title('stem');
nexttile; scatter(randn(1,150), randn(1,150), 25, 'filled'); title('scatter');
exportgraphics(f, [out '/example-tiled.png'], 'Resolution', 100); close(f);
%% 3. surf / mesh
f = figure('visible','off','Position',[100 100 900 420]);
[X, Y] = meshgrid(-2:0.15:2);
Z = X .* exp(-X.^2 - Y.^2);
subplot(1,2,1); surf(X, Y, Z); title('surf'); colormap('parula'); colorbar;
subplot(1,2,2); mesh(X, Y, peaks(numel(X(1,:)))*0.1); title('mesh (peaks)');
exportgraphics(f, [out '/example-surf.png'], 'Resolution', 100); close(f);
%% 4. histogram / bar / pie
f = figure('visible','off','Position',[100 100 1000 360]);
subplot(1,3,1); hist(randn(1,2000), 30); title('hist');
subplot(1,3,2); bar([3 5 2 7 4]); title('bar'); set(gca,'XTick',1:5);
subplot(1,3,3); pie([30 20 25 25]); title('pie');
exportgraphics(f, [out '/example-bar-pie.png'], 'Resolution', 100); close(f);
%% 5. imagesc with colormap
f = figure('visible','off','Position',[100 100 640 500]);
imagesc(peaks(80)); colormap('jet'); colorbar; axis image;
title('imagesc(peaks(80)) with jet colormap');
exportgraphics(f, [out '/example-imagesc.png'], 'Resolution', 100); close(f);
disp('gallery done');
