% IDE demo: surface + debugger
[X, Y] = meshgrid(linspace(-2, 2, 40));
Z = X .* exp(-X.^2 - Y.^2);
total = 0;
for k = 1:5
  total = total + k;
end
surf(X, Y, Z); shading interp; colormap(jet);
title('IDE demo');
fprintf('total = %d\n', total);
